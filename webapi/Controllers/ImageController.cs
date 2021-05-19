using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;

using System.IO;
namespace webapi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MediaController : ControllerBase
    {

        [HttpPost]
        public async Task<IActionResult> Post()
        {
            // リクエストボディを文字列として読み込む
            // このときはまだ{key}={value}
            string body;
            using (var reader = new StreamReader(HttpContext.Request.Body))
            {
                body = await reader.ReadToEndAsync();
            }

            // 受け取ったリクエストボディをパースするよ
            // {key}={value}が望ましいけど{key}={空っぽ}とか{key}={base64}とかも対処するよ
            var queryPair = body.Split("&");
            foreach(var pair in queryPair){
                Console.WriteLine($"{pair}");
            }
            
            var query = new List<KeyValuePair<string, string>>();
            foreach (var q in queryPair)
            {
                var tmp = q.Split("=", 2);
                if (tmp.Count() == 2)
                {
                    query.Add(new KeyValuePair<string, string>(tmp[0], tmp[1]));
                }
            }


            var receivedData = query.Where(x => x.Key == "data");
            String data;
            if (receivedData.Count() == 1)
            {
                data = receivedData.First().Value;
            }
            else
            {
                return BadRequest(new { reason = "nothing or multiple data is provided." });
            }

            var receivedType = query.Where(x => x.Key == "type");
            String type;
            if (receivedType.Count() == 1)
            {
                type = receivedType.First().Value;
            }
            else
            {
                return BadRequest(new { reason = "nothing or multiple media type is provided" });
            }

            // 拡張子を設定するよ〜〜〜〜
            System.Drawing.Imaging.ImageFormat format;
            switch (type.ToLower())
            {
                case "png":
                    format = System.Drawing.Imaging.ImageFormat.Png;
                    break;
                case "jpg":
                case "jpeg":
                    format = System.Drawing.Imaging.ImageFormat.Jpeg;
                    break;
                case "gif":
                    format = System.Drawing.Imaging.ImageFormat.Gif;
                    break;
                default:
                    return BadRequest(new { reason = "unsupported media type" });
            }

            // {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}.{拡張子}
            var filename = $"{Guid.NewGuid().ToString("D")}.{format.ToString().ToLower()}";


            // 文字列として受け取った画像ファイルをbyte列に変換
            var byteData = new List<byte>();
            for (var i = 0; i < data.Length; i += 2)
            {
                byteData.Add(Convert.ToByte(data.Substring(i, 2), 16));
            }

            // GUIDの衝突がめちゃくちゃ稀に、ほんと稀に存在するかもしれないくらいのアレなので念の為削除
            if (System.IO.File.Exists($"media/{filename}"))
            {
                System.IO.File.Delete($"media/{filename}");
            }

            // 保存
            using (var image = System.Drawing.Image.FromStream(new MemoryStream(byteData.ToArray())))
            {
                image.Save($"media/{filename}", format);
            }

            ulong mediaId = 0;
            using (var connection = new MySqlConnection("server=localhost;userid=qiita;password=qiita;database=qiita;"))
            {
                {
                    // ファイルを登録するよ
                    var cmd = new MySqlCommand($"insert into media(media_path) values ('{filename}')", connection);
                    cmd.Connection.Open();
                    cmd.ExecuteNonQuery();
                    cmd.Connection.Close();
                }

                {
                    // 昇順で自動採番されるのでもう一度接続して最大値を取り出すよ
                    var cmd = new MySqlCommand($"select max(media_id) as max from media", connection);
                    cmd.Connection.Open();
                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        mediaId = ulong.Parse(reader["max"].ToString());
                    }
                    cmd.Connection.Close();

                }
            }

            return Ok(new { media_id = mediaId });
        }




        [HttpGet]
        public ActionResult Get()
        {
            var media_id = long.Parse(HttpContext.Request.Query["media_id"].ToString());
            Console.WriteLine($"{media_id}");
            var media_path = "";

            using (var connection = new MySqlConnection("server=localhost;userid=qiita;password=qiita;database=qiita;"))
            {
                var cmd = new MySqlCommand($"select media_path from media where media_id = {media_id}", connection);

                cmd.Connection.Open();
                var reader = cmd.ExecuteReader();

                if (!reader.HasRows)
                {
                    return NotFound(new { reason = "specified media id does not exist" });
                }

                while (reader.Read())
                {
                    media_path = reader["media_path"].ToString();
                }

                cmd.Connection.Close();
            }
            Console.WriteLine($"media/{media_path}");
            Console.WriteLine($"image/{Path.GetExtension(media_path)}");
            var file = new BinaryReader(new StreamReader($"media/{media_path}").BaseStream);
            var stream = new MemoryStream(file.ReadBytes((int)file.BaseStream.Length));

            return File(stream, $"image/{Path.GetExtension(media_path).Trim('.')}");
        }
    }
}
