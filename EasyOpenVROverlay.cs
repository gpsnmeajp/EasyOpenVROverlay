/*
 * EasyOpenVROverlay by gpsnmeajp v0.1
 * 2019/07/01
 * 
 * v0.1 公開 
 * 
 * These codes are licensed under CC0.
 * http://creativecommons.org/publicdomain/zero/1.0/deed.ja
 */

 using System;
using System.IO;
using System.Diagnostics;
using System.Numerics;
using Valve.VR;

namespace EasyOpenVROverlay
{
    class Overlay
    {
        //オーバーレイのハンドル(整数)
        ulong overlayHandle = INVALID_HANDLE;

        //OpenVRシステムインスタンス
        CVRSystem openvr = null;

        //Overlayインスタンス
        CVROverlay overlay = null;

        //HMD視点位置変換行列
        HmdMatrix34_t p;

        //無効なハンドル
        const ulong INVALID_HANDLE = 0;

        //オーバーレイに渡すネイティブテクスチャ
        Texture_t overlayTexture;

        //Unity準拠の位置と回転
        Vector3 Position = new Vector3(0, 0, 2);
        Vector3 Rotation = new Vector3(0, 0, 0);
        Vector3 Scale = new Vector3(1, 1f, 1);
        //鏡像反転できるように
        bool MirrorX = false;
        bool MirrorY = false;

        //絶対空間か
        bool DeviceTracking = true;
        //(絶対空間の場合)ルームスケールか、着座状態か
        bool Seated = false;


        //追従対象デバイス。HMD=0
        //public uint DeviceIndex = OpenVR.k_unTrackedDeviceIndex_Hmd;
        TrackingDeviceSelect DeviceIndex = TrackingDeviceSelect.HMD;

        public enum UpdateStatus
        {
            Enable,
            Disable
        }
        public enum TextureFormat
        {
            OpenGL,
            DirectX
        }

        public enum TrackingDeviceSelect
        {
            None = -99,
            RightController = -2,
            LeftController = -1,
            HMD = (int)OpenVR.k_unTrackedDeviceIndex_Hmd,
            Device1 = 1,
            Device2 = 2,
            Device3 = 3,
            Device4 = 4,
            Device5 = 5,
            Device6 = 6,
            Device7 = 7,
            Device8 = 8,
        }

        public Overlay() {

        }

        ~Overlay()
        {
            Dispose();
        }

        //オーバーレイを破棄
        public void Dispose()
        {
            //ハンドルを解放
            if (overlayHandle != INVALID_HANDLE && overlay != null)
            {
                overlay.DestroyOverlay(overlayHandle);
            }

            overlayHandle = INVALID_HANDLE;
            overlay = null;
            openvr = null;
        }

        //エラー状態かを確認
        public bool IsError()
        {
            return overlayHandle == INVALID_HANDLE || overlay == null || openvr == null;
        }

        //OpenVRが起動状態かをチェック
        public static bool IsOpenVRunning()
        {
            try
            {
                Process[] p = Process.GetProcessesByName("vrcompositor");
                if (p.Length == 0)
                {
                    //起動してない
                    return false;
                }
            }
            catch (Exception)
            {
                //エラー
                return false;
            }
            return true;
        }

        public void Initialize(string name, string key, TextureFormat format)
        {
            var openVRError = EVRInitError.None;
            var overlayError = EVROverlayError.None;

            //OpenVRの初期化
            openvr = OpenVR.Init(ref openVRError, EVRApplicationType.VRApplication_Overlay);
            if (openVRError != EVRInitError.None)
            {
                Dispose();
                throw new IOException(openVRError.ToString());
            }

            //オーバーレイ機能の初期化
            overlay = OpenVR.Overlay;
            overlayError = overlay.CreateOverlay(name, key, ref overlayHandle);
            if (overlayError != EVROverlayError.None)
            {
                Dispose();
                throw new IOException(overlayError.ToString());
            }

            //オーバーレイに渡すテクスチャ種類の設定
            if (format == TextureFormat.OpenGL)
            {
                //pGLuintTexture
                overlayTexture.eType = ETextureType.OpenGL;
                //上下反転しない
                SetTextureBounds(0, 0, 1, 1);
            }
            else
            {
                //pTexture
                overlayTexture.eType = ETextureType.DirectX;
                //上下反転する
                SetTextureBounds(0, 1, 1, 0);
            }

            return;
        }

        public void SetTextureBounds(float uMin, float vMin, float uMax, float vMax)
        {
            var OverlayTextureBounds = new VRTextureBounds_t();
            OverlayTextureBounds.uMin = uMin;
            OverlayTextureBounds.vMin = vMin;
            OverlayTextureBounds.uMax = uMax;
            OverlayTextureBounds.vMax = vMax;
            overlay.SetOverlayTextureBounds(overlayHandle, ref OverlayTextureBounds);
        }

        public void SetShow(bool show)
        {
            if (show)
            {
                //オーバーレイを表示する
                overlay.ShowOverlay(overlayHandle);
            }
            else
            {
                //オーバーレイを非表示にする
                overlay.HideOverlay(overlayHandle);
            }
        }

        public void SetPosition(Vector3 pos, UpdateStatus update = UpdateStatus.Enable)
        {
            Position = pos;
            if (update == UpdateStatus.Enable) {
                UpdatePosition();
            }
        }
        public void SetRotation(Vector3 rot, UpdateStatus update = UpdateStatus.Enable)
        {
            Rotation = Vector3.Multiply((float)Math.PI / 180f,rot);
            if (update == UpdateStatus.Enable)
            {
                UpdatePosition();
            }
        }
        public void SetScale(Vector3 scale, UpdateStatus update = UpdateStatus.Enable)
        {
            Scale = scale;
            if (update == UpdateStatus.Enable)
            {
                UpdatePosition();
            }
        }
        public void SetMirror(bool X, bool Y, UpdateStatus update = UpdateStatus.Enable)
        {
            MirrorX = X;
            MirrorY = Y;
            if (update == UpdateStatus.Enable)
            {
                UpdatePosition();
            }
        }
        public void SetAlpha(float alpha)
        {
            //オーバーレイの透明度を設定
            overlay.SetOverlayAlpha(overlayHandle, alpha);
        }
        public void SetWidth(float width)
        {
            //オーバーレイの大きさ設定(幅のみ。高さはテクスチャの比から自動計算される)
            overlay.SetOverlayWidthInMeters(overlayHandle, width);
        }
        public void SetMouseScale(float X, float Y)
        {
            //マウスカーソルスケールを設定する(これにより表示領域のサイズも決定される)
            HmdVector2_t vecMouseScale = new HmdVector2_t
            {
                v0 = X,
                v1 = Y
            };
            overlay.SetOverlayMouseScale(overlayHandle, ref vecMouseScale);

        }
        public void SetTextureFromFile(string filename)
        {
            var overlayError = EVROverlayError.None;
            overlayError = overlay.SetOverlayFromFile(overlayHandle, filename);
            if (overlayError != EVROverlayError.None)
            {
                throw new IOException(overlayError.ToString());
            }
        }
        public void SetDeviceTracking(bool tracking, bool seated = false)
        {
            DeviceTracking = tracking;
            Seated = seated;
        }
        public void SetDeviceTrackingIndex(TrackingDeviceSelect device)
        {
            DeviceIndex = device;
        }
        public void ResetSeatedPosition()
        {
            OpenVR.System.ResetSeatedZeroPose();
        }

        public bool ProcessEvent(bool debug)
        {
            //イベント構造体のサイズを取得
            uint uncbVREvent = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(VREvent_t));

            //イベント情報格納構造体
            VREvent_t Event = new VREvent_t();
            //イベントを取り出す
            while (overlay.PollNextOverlayEvent(overlayHandle, ref Event, uncbVREvent))
            {
                //イベントのログを表示
                if (debug)
                {
                    Console.WriteLine("Event:" + ((EVREventType)Event.eventType).ToString());
                }

                //イベント情報で分岐
                switch ((EVREventType)Event.eventType)
                {
                    case EVREventType.VREvent_Quit:
                        return true;
                }
            }
            return false;
        }

        public void UpdatePosition()
        {
            //回転を生成
            Quaternion quaternion = Quaternion.CreateFromYawPitchRoll(Rotation.X, Rotation.Y, Rotation.Z);
            //座標系を変更(右手系と左手系の入れ替え)
            Vector3 position = Position;
            position.Z = -Position.Z;
            //HMD視点位置変換行列に書き込む。
            Matrix4x4 t = Matrix4x4.CreateTranslation(position);
            Matrix4x4 r = Matrix4x4.CreateFromQuaternion(quaternion);
            Matrix4x4 s = Matrix4x4.CreateScale(Scale);

            Matrix4x4 m = s * r * t;
            //鏡像反転
            Vector3 Mirroring = new Vector3(MirrorX ? -1 : 1, MirrorY ? -1 : 1, 1);

            //4x4行列を3x4行列に変換する。
            p.m0 = Mirroring.X * m.M11; p.m1 = Mirroring.Y * m.M21; p.m2 = Mirroring.Z * m.M31; p.m3 = m.M41;
            p.m4 = Mirroring.X * m.M12; p.m5 = Mirroring.Y * m.M22; p.m6 = Mirroring.Z * m.M32; p.m7 = m.M42;
            p.m8 = Mirroring.X * m.M13; p.m9 = Mirroring.Y * m.M23; p.m10 = Mirroring.Z * m.M33; p.m11 = m.M43;


            //回転行列を元に相対位置で表示
            if (DeviceTracking)
            {
                //deviceindexを処理(コントローラーなどはその時その時で変わるため)
                var idx = OpenVR.k_unTrackedDeviceIndex_Hmd;
                switch (DeviceIndex)
                {
                    case TrackingDeviceSelect.LeftController:
                        idx = openvr.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand);
                        break;
                    case TrackingDeviceSelect.RightController:
                        idx = openvr.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand);
                        break;
                    default:
                        idx = (uint)DeviceIndex;
                        break;
                }

                //HMDからの相対的な位置にオーバーレイを表示する。
                overlay.SetOverlayTransformTrackedDeviceRelative(overlayHandle, idx, ref p);
            }
            else
            {
                //空間の絶対位置にオーバーレイを表示する
                if (!Seated)
                {
                    overlay.SetOverlayTransformAbsolute(overlayHandle, ETrackingUniverseOrigin.TrackingUniverseStanding, ref p);
                }
                else
                {
                    overlay.SetOverlayTransformAbsolute(overlayHandle, ETrackingUniverseOrigin.TrackingUniverseSeated, ref p);
                }
            }

        }
    }
}
