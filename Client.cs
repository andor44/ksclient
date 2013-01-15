using System;
using ZMQ;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using System.Diagnostics;
using LZ4Sharp;
using System.IO;

namespace KSClient
{
    public enum CompressionType
    {
        Uncompressed,
        LZ4
    }
    public enum ImageType
    {
        Depth,
        Image,
        IR // unsupported on linux with x360 kinect?
    }
    public class Client
    {
        Bitmap bmp;
        BitmapData bmp_d;

        ZMQ.Context ctx;
        ZMQ.Socket sock;
        ILZ4Decompressor dcmp;

        byte[] buf;
        private const int COMPR_TYPE_NONE = 1;
        private const int COMPR_TYPE_LZ4 = 2;

        private const int IMG_TYPE_DEPTH = 1;
        private const int IMG_TYPE_IMAGE = 2;
        private const int IMG_TYPE_IR = 3;

        private const int depth_bpp = 2;
        private const int image_bpp = 3;
        private const int ir_bpp = 1;


        public Client(string address, int port = 4949)
        {
            ctx = new Context(4);
            sock = ctx.Socket(SocketType.REQ);

            // TODO: set me
            bmp = new Bitmap(640, 480, PixelFormat.Format16bppRgb555);

            sock.Connect("tcp://"+address+":"+port.ToString());

            buf = new byte[640*480*3]; // w*h*BYTES PER PIXEL!

            dcmp = new LZ4Decompressor64(); // returns appropriate sized decompressor

            decompressed = new byte[640 * 480 * 3];
        }

        public Bitmap GetBitmap()
        {
            return bmp;
        }

        byte[] decompressed;

        public void RequestImage(ImageType img_type, CompressionType comp_type = CompressionType.LZ4)
        {
            byte[] send_params = new byte[2];

            switch (comp_type)
            {
                case CompressionType.Uncompressed:
                    send_params[0] = COMPR_TYPE_NONE;
                    break;
                case CompressionType.LZ4:
                    send_params[0] = COMPR_TYPE_LZ4;
                    break;
                default:
                    break;
            }

            switch (img_type)
            {
                case ImageType.Depth:
                    send_params[1] = IMG_TYPE_DEPTH;
                    break;
                case ImageType.Image:
                    send_params[1] = IMG_TYPE_IMAGE;
                    break;
                case ImageType.IR:
                    send_params[1] = IMG_TYPE_IR;
                    break;
                default:
                    break;
            }

            sock.Send(send_params);
            
            buf = sock.Recv();

            switch (comp_type)
            {
                case CompressionType.Uncompressed:
                    buf.CopyTo(decompressed, 0);
                    break;
                case CompressionType.LZ4:
                    dcmp.DecompressKnownSize(buf, decompressed, 640 * 480 * (img_type == ImageType.Depth ? depth_bpp : (img_type == ImageType.Image ? image_bpp : ir_bpp)));
                    break;
                default:
                    break;
            }
            

            bmp_d = bmp.LockBits(new Rectangle(0, 0, 640, 480), ImageLockMode.WriteOnly, 
                (img_type == ImageType.Depth ? PixelFormat.Format16bppRgb555 : (img_type == ImageType.Image ? PixelFormat.Format24bppRgb : PixelFormat.Format8bppIndexed)));

        // http://msdn.microsoft.com/en-us/library/system.drawing.imaging.bitmapdata.aspx

        IntPtr ptr = bmp_d.Scan0;
        // Copy the RGB values back to the bitmap
        System.Runtime.InteropServices.Marshal.Copy(decompressed, 0, ptr, bmp_d.Width * bmp_d.Height * (img_type == ImageType.Depth ? depth_bpp : (img_type == ImageType.Image ? image_bpp : ir_bpp))); // elvileg mindenképp 640x480x2, fixme

        // Unlock the bits.
        bmp.UnlockBits(bmp_d);
        // elvileg ez a leggyorsabb mód a másolásra, ennél gyorsabban nem lehet...
        }
    }
}

