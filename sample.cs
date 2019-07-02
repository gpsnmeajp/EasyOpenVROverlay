using System;
using System.Numerics;
using EasyOpenVROverlay;

namespace OverlayTest1
{
    class Program
    {
        static void Main(string[] args)
        {
            var eovro = new Overlay();
            //初期化
            eovro.Initialize("HelloOverlay", "HelloOverlay", Overlay.TextureFormat.OpenGL);

            //初期位置を設定
            eovro.SetPosition(new Vector3(0, 0, 0.48f));
            eovro.SetWidth(0.30f);
            eovro.SetAlpha(1.0f);

            //画像を設定
            eovro.SetTextureFromFile(@"E:\a.png");
            eovro.SetShow(true);
            Console.ReadKey();

            //破棄
            eovro.Dispose();
        }
    }
}
