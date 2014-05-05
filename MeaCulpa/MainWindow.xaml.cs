using Emgu.CV;
using Emgu.CV.Structure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WpfApplication1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int EYE_HISTORY_WINDOW = 50;
        private const int EYE_CACHE_THRESHOLD = 10;
        private const int EYE_DISTANCE_THRESHOLD = 100;
        private object eyeAvgLock;
        private int currentCacheIteration = 0;

        /// <summary>
        /// Keeps record of the eye rects but keeping a count of the last N
        /// eye detection rectangles that intersect with the last.
        /// 
        /// Example
        /// 1st iteration: 2 rects identified as eyes. They are added as Tuple<rect, 0>
        ///     eyesRectAvg=[Tuple<rect1, 0>, Tuple<rect2, 0>]
        /// 2nd iteration: 3 rects identified. 1&2 intersect with last iteration rects. So
        ///     the intersected rects in cache are updated by replacing them with the latest rect with +1 in its tuple value
        ///     and a new rect(the one with no match) is added:
        ///     eyesRectAvg=[Tuple<rect3, 1>, Tuple<rect4, 1>, Tuple<rect3, 0>]
        ///     
        /// This will give better accuracy to detect eyes as sometimes for a couple ms something in screen is detected as an eye
        /// but its quickly thrown away (not detected again). We can assume that, in avergae, the rects that stay detected for 
        /// the last N iterations are eyes with high confidence.
        /// </summary>
        private List<List<System.Drawing.Rectangle>> eyesRectAvg;

        public MainWindow()
        {
            InitializeComponent();

            Tuple<System.Drawing.Rectangle, int> n = new Tuple<System.Drawing.Rectangle, int>(
                new System.Drawing.Rectangle(),
                0);

            eyesRectAvg = new List<List<System.Drawing.Rectangle>>();
            eyeAvgLock = new object();
            capture = new Capture();
            haarCascade = new HaarCascade(@"c:\users\fernando\documents\visual studio 2013\Projects\WpfApplication1\WpfApplication1\haarcascade_eye.xml");
            var timer = new DispatcherTimer();
            timer.Tick += new EventHandler(timer_Tick);
            timer.Interval = new TimeSpan(0, 0, 0, 0, 1);
            timer.Start();
        }

        Capture capture;
        HaarCascade haarCascade;

        void timer_Tick(object sender, EventArgs e)
        {
            Image<Bgr, Byte> currentFrame = capture.QueryFrame();

            if (currentFrame != null)
            {
                Image<Gray, Byte> grayFrame = currentFrame.Convert<Gray, Byte>();

                var detectedFaces = haarCascade.Detect(
                    grayFrame,
                    1.2, 
                    20, 
                    Emgu.CV.CvEnum.HAAR_DETECTION_TYPE.DO_CANNY_PRUNING,
                    new System.Drawing.Size(5, 5),
                    new System.Drawing.Size(150, 150));

                ProcessEyesRectAvg(currentFrame, detectedFaces.ToList().Select(face => face.rect));
            }

        }

        private void ProcessEyesRectAvg(Image<Bgr, Byte> currentFrame, IEnumerable<System.Drawing.Rectangle> facesRect)
        {
            lock (eyeAvgLock)
            {
                if (facesRect.Any() && facesRect.Count() >= EYE_HISTORY_WINDOW) { eyesRectAvg.RemoveAt(0); }
                if (eyesRectAvg.Count >= EYE_HISTORY_WINDOW) eyesRectAvg.RemoveAt(0);
                if (facesRect.Any()) eyesRectAvg.Add(facesRect.ToList());

                var rectsToPrint = new List<System.Drawing.Rectangle>();
                foreach (var faceRect in facesRect)
                {
                    var cacheMatches = eyesRectAvg
                        .SelectMany(eyeAvg => eyeAvg)
                        .Where(eyeAvg => System.Drawing.Rectangle.Intersect(faceRect, eyeAvg) != System.Drawing.Rectangle.Empty)
                        .Count();

                    if (cacheMatches > EYE_CACHE_THRESHOLD)
                    {
                        rectsToPrint.Add(faceRect);
                    }
                }
                
                var twoEyesOnly = eyesRectAvg.Where(eyesRect => eyesRect.Count == 2);
                if (twoEyesOnly.Any())
                {
                    var eyesDistanceAvg = twoEyesOnly.Average(eyes => Math.Abs((eyes[0].X + eyes[0].Width / 2) - (eyes[1].X + eyes[1].Width / 2)));

                    Idistance.Content = eyesDistanceAvg.ToString("F2");
                    if (eyesDistanceAvg > EYE_DISTANCE_THRESHOLD)
                    {
                        fuckoff.Content = "Woah dude!! WTF I just said a little closer... FUCK OFF!!!!... I love you <3";
                    }
                    else
                    {
                        fuckoff.Content = "Hey I think I know you, can you come a little closer...";
                    }
                }

                rectsToPrint.ForEach(face => currentFrame.Draw(face, new Bgr(0, double.MaxValue, 0), 3));
                image1.Source = ToBitmapSource(currentFrame);
            }
        }

        [DllImport("gdi32")]
        private static extern int DeleteObject(IntPtr o);

        public static BitmapSource ToBitmapSource(IImage image)
        {
            using (System.Drawing.Bitmap source = image.Bitmap)
            {
                IntPtr ptr = source.GetHbitmap(); //obtain the Hbitmap

                BitmapSource bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    ptr,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

                DeleteObject(ptr); //release the HBitmap
                return bs;
            }
        }

    }
}
