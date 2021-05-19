package com.milkcocoa.info.client

import android.app.Activity
import android.content.Intent
import android.graphics.Bitmap
import android.net.Uri
import android.os.Bundle
import android.util.Log
import android.widget.Button
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import com.bumptech.glide.Glide
import com.github.kittinunf.fuel.gson.responseObject
import com.github.kittinunf.fuel.httpPost
import com.yalantis.ucrop.UCrop
import com.yalantis.ucrop.model.AspectRatio
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import java.io.BufferedInputStream
import java.io.File
import java.util.*

class ActivityMain: AppCompatActivity() {
    var selectedImageUri: Uri? = null
    var cropImageUri: Uri? = null
    lateinit var select: Button
    lateinit var upload: Button
    lateinit var download: Button

    data class Media(val media_id: ULong)
    var media: Media? = null

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)

        // ファイル選択用のやつ
        // startActivityForResultとonActivityResultはdeprecatedなのでこちらを使いましょう。
        val chooser = registerForActivityResult(ActivityResultContracts.GetContent()) { uri ->
            if (uri == null) {
                return@registerForActivityResult
            }
            selectedImageUri = uri
            val tmpFileName = UUID.randomUUID().toString() + ".png"
            File.createTempFile(tmpFileName, null, cacheDir)
            val tmpFileUri = Uri.fromFile(File(cacheDir, tmpFileName))

            // UCropの設定をします。詳しくはリファレンスを見てくれ
            var options = com.yalantis.ucrop.UCrop.Options()
            options.setToolbarTitle("切り抜き")
            options.setCompressionFormat(Bitmap.CompressFormat.PNG)
            options.setAspectRatioOptions(0, AspectRatio("3:4", 3.0f, 4.0f));
            options.setCompressionQuality(70)
            var uCrop = UCrop.of(selectedImageUri!!, tmpFileUri)
            uCrop.withOptions(options)
            uCrop.start(this)

        }

        // 画像選択ボタン
        select = findViewById<Button>(R.id.select).also {
            it.setOnClickListener {
                upload.isEnabled = false
                download.isEnabled = false
                chooser.launch("image/*")
            }
        }

        // 画像アップロードボタン
        upload = findViewById<Button>(R.id.upload).also {
            it.setOnClickListener {
                val stream =
                    BufferedInputStream(
                        applicationContext.getContentResolver().openInputStream(cropImageUri!!)
                    )

                val size = stream.available()
                val data = ByteArray(size)
                val builder = StringBuilder()


                // 画像データをHexデコード
                stream.read(data)
                for (byte in data) {
                    builder.append("%02X".format(byte))
                }

                // Androidの規格上、メインスレッドで通信できないのでIOスレッドに逃がす
                CoroutineScope(Dispatchers.IO).launch{
                    "http://10.0.2.2:5001/media"
                        .httpPost(listOf("data" to builder.toString(), "type" to "png")) // データとタイプをセット
                        .responseObject<Media> { _, _, result ->
                            val (media, err) = result
                            this@ActivityMain.media = media
                            CoroutineScope(Dispatchers.Main).launch {
                                download.isEnabled = true
                            }
                        }
                }
            }
        }

        // 画像ダウンロードボタン
        download = findViewById<Button>(R.id.download).also {
            it.setOnClickListener {
                Glide.with(this)
                    .load("http://10.0.2.2:5001/media?media_id=${media?.media_id}")
                    .into(findViewById(R.id.uploaded_image))
            }
        }
    }


    override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
        // UCropでの切り抜き結果に対する処理
        if ((requestCode and 0xff) == UCrop.REQUEST_CROP) {
            if (resultCode == Activity.RESULT_OK) {
                data?.let {
                    cropImageUri = UCrop.getOutput(it)
                    Glide.with(this).load(UCrop.getOutput(it))
                        .into(findViewById(R.id.selected_image))
                    upload.isEnabled = true
                }
            } else if (resultCode == UCrop.RESULT_ERROR) {
                Log.e("TAG", "uCropエラー: " + UCrop.getError(data!!).toString())
            }
        }
        super.onActivityResult(requestCode, resultCode, data)
    }
}