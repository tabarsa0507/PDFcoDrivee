using Microsoft.AspNetCore.Mvc;
using PDFcoDrive.Services;
using SkiaSharp;

namespace PDFcoDrive.Controllers
{
    public class CaptchaController : Controller
    {
        private readonly ICaptchaService _captcha;

        public CaptchaController(ICaptchaService captcha)
        {
            _captcha = captcha;
        }

        [HttpGet]
        public IActionResult Image()
        {
            var code = _captcha.GenerateCode(5);
            HttpContext.Session.SetString("CaptchaCode", _captcha.HashCode(code));

            var bytes = GenerateImage(code);
            return File(bytes, "image/png");
        }

        private byte[] GenerateImage(string code)
        {
            const int width = 180;
            const int height = 60;

            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            var canvas = surface.Canvas;

            // پس‌زمینه تیره (Dark Pro)
            canvas.Clear(new SKColor(0x1a, 0x1f, 0x3a));

            // خطوط تزئینی (آبی نئون)
            using var linePaint = new SKPaint
            {
                Color = new SKColor(0x00, 0xd4, 0xff, 60),
                StrokeWidth = 1.5f,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };

            var rnd = new Random();
            for (int i = 0; i < 5; i++)
            {
                canvas.DrawLine(rnd.Next(width), rnd.Next(height),
                    rnd.Next(width), rnd.Next(height), linePaint);
            }

            // نقاط تزئینی
            using var dotPaint = new SKPaint
            {
                Color = new SKColor(0x00, 0xd4, 0xff, 100),
                IsAntialias = true
            };
            for (int i = 0; i < 80; i++)
            {
                canvas.DrawCircle(rnd.Next(width), rnd.Next(height), 1f, dotPaint);
            }

            // نوشتن کد (آبی نئون)
            using var font = new SKFont(SKTypeface.Default, 32);
            using var textPaint = new SKPaint
            {
                Color = new SKColor(0x00, 0xd4, 0xff),
                IsAntialias = true
            };

            float x = 15;
            foreach (var ch in code)
            {
                canvas.Save();
                float y = 42 + rnd.Next(-4, 4);
                canvas.Translate(x + 14, y - 10);
                canvas.RotateDegrees(rnd.Next(-15, 15));
                canvas.Translate(-(x + 14), -(y - 10));
                canvas.DrawText(ch.ToString(), x, y, SKTextAlign.Left, font, textPaint);
                canvas.Restore();
                x += 30;
            }

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
    }
}