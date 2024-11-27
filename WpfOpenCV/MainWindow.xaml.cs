using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.Internal;
using System.Formats.Tar;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.IO;
using Microsoft.Win32;
using System;

namespace WpfOpenCV
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
	{
		// image matrix
		string filePath;
		Mat inImage;
		Mat outPhase;
		Mat outMag;

		// variables for mouse state
		bool bMouseDown = false;

		// variables for result image
		const int halfSize = 10; // Crop Region: (halfSize*2+1) X (halfSize*2+1)
		const int scaleFactor = 50;

		// variables for drawing arrow
		const double arrowLength = 8.0;
		const double arrowAngle = 30.0;

		// variables for drawing
		const int lineThick = 2;
		const int arrowThick = 2;

		// variables for filter
		int filterType = 0; // 0: sobel, 1: scharr
		const int ksize = 3; // sobel
		const int scale = 1; // sobel, scharr
		const int delta = 1; // sobel, scharr
		const OpenCvSharp.BorderTypes border = BorderTypes.Default; // sobel, scharr

		public MainWindow()
        {
            InitializeComponent();
		}

		// create magnitude & phase image
		private void InitImage()
		{
			if (filePath == null) return;

			PathText.Text = filePath;
			inImage = Cv2.ImRead(filePath, ImreadModes.Grayscale);
			OriginalImage.Source = OpenCvSharp.WpfExtensions.BitmapSourceConverter.ToBitmapSource(inImage);

			Mat filterX = new Mat(inImage.Size(), MatType.CV_32F); // dx filter
			Mat filterY = new Mat(inImage.Size(), MatType.CV_32F); // dy filter
			outMag = new Mat(inImage.Size(), MatType.CV_8UC1);
			outPhase = new Mat(inImage.Size(), MatType.CV_32F);

			if (radioSobel.IsChecked == true)		filterType = 0;
			else if (radioScharr.IsChecked == true)	filterType = 1;

			// image filter
			switch (filterType)
			{
				case 0: // sobel
					Cv2.Sobel(inImage, filterX, MatType.CV_32F, 1, 0, ksize: ksize, scale: scale, delta: delta, borderType: border);
					Cv2.Sobel(inImage, filterY, MatType.CV_32F, 0, 1, ksize: ksize, scale: scale, delta: delta, borderType: border);
					break;
				case 1: // scharr
					Cv2.Scharr(inImage, filterX, MatType.CV_32F, 1, 0, scale: scale, delta: delta, border);
					Cv2.Scharr(inImage, filterY, MatType.CV_32F, 0, 1, scale: scale, delta: delta, border);
					break;
			}

			// get magnitude and phase
			Cv2.Magnitude(filterX, filterY, outMag);
			outMag.ConvertTo(outMag, MatType.CV_8UC1);
			Cv2.Phase(filterX, filterY, outPhase, true);
		}


		/*
		 * Widget Event
		 */

		// search button click... >> file select
		private void Search_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.Filter = "BMP files (*.bmp)|*.bmp|JPG files (*.jpg)|*.jpg|JPEG files (*.jpeg)|*.jpeg|PNG files (*.png)|*.png|All files (*.*)|*.*"; 
			if (openFileDialog.ShowDialog() == true)
			{
				PathText.Text = File.ReadAllText(openFileDialog.FileName);
				filePath = openFileDialog.FileName;
				InitImage();
			}
		}

		private void Radio_Sobel(object sender, RoutedEventArgs e)
		{
			InitImage();
		}
		private void Radio_Scharr(object sender, RoutedEventArgs e)
		{
			InitImage();
		}
		
		
		/*
		 * Mouse State
		 */
		private void Pic_MouseDown(object sender, MouseEventArgs e) { bMouseDown = true; }
		private void Pic_MouseUp(object sender, MouseEventArgs e)	{ bMouseDown = false; }
		private void Pic_MouseMove(object sender, MouseEventArgs e)
		{
			if (bMouseDown)
			{
				Run(e.GetPosition(OriginalImage)); 
			}
		}

		// crop image and draw lines
		private void Run(System.Windows.Point pt)
		{
			// calculate position
			int startX = Math.Max((int)pt.X - halfSize, 0);
			int startY = Math.Max((int)pt.Y - halfSize, 0);
			int endX = Math.Min((int)pt.X + halfSize, inImage.Width - 1);
			int endY = Math.Min((int)pt.Y + halfSize, inImage.Height - 1);

			int width = endX - startX;
			int height = endY - startY;

			if (width <= 0 || height <= 0)
				return;

			// get region
			OpenCvSharp.Rect region = new OpenCvSharp.Rect(startX, startY, width, height);
			Mat inCropImage = new Mat(inImage, region);
			Mat inCropMag = new Mat(outMag, region);
			Mat inCropPhase = new Mat(outPhase, region);
			Mat outImage = new Mat();

			Cv2.Resize(inCropImage, outImage,
				new OpenCvSharp.Size(inCropImage.Width * scaleFactor, inCropImage.Height * scaleFactor),
				0, 0, InterpolationFlags.Nearest);

			for (int i = 0; i < inCropImage.Height; i++)
			{
				for (int j = 0; j < inCropImage.Width; j++)
				{
					if (i > inCropMag.Height || j > inCropMag.Width)
						continue;

					// get startpoint
					OpenCvSharp.Point startPoint = new OpenCvSharp.Point(
						((scaleFactor / 2) * j) + (scaleFactor / 2) * (j + 1),
						((scaleFactor / 2) * i) + (scaleFactor / 2) * (i + 1)
					);

					// vector size(0~255) ---Normalize---> 0~scaleFactor/2
					double magnitude = Normalize(inCropMag.At<byte>(i, j), 0, 255, 0, scaleFactor / 2); 
					// vector angle (degree)
					double angle = inCropPhase.At<float>(i, j);
					// degree to radian
					double angleRad = angle * Math.PI / 180.0;

					// get endpoint
					// polar -> cartesian coords
					OpenCvSharp.Point endPoint = new OpenCvSharp.Point(
						startPoint.X + (int)(magnitude * Math.Cos(angleRad)), // magnitude * rad
						startPoint.Y - (int)(magnitude * Math.Sin(angleRad)) // y-axis is opposite. (as it goes down, the position gets higher in image process.)
					);

					// if uses only one color, it may not be visible.
					// so, process two colors according to below conditions.
					int pixelValue = inCropImage.At<byte>(i, j);
					Scalar color;
					if (pixelValue > 0 && pixelValue < 128)
						color = OpenCvSharp.Scalar.FromRgb(255, 255, 255);
					else
						color = OpenCvSharp.Scalar.FromRgb(0, 0, 0);


					// get arrow endpoint
					OpenCvSharp.Point arrowPoint1 = new OpenCvSharp.Point(
						endPoint.X - (int)(arrowLength * Math.Cos(angleRad - arrowAngle * Math.PI / 180.0)),
						endPoint.Y + (int)(arrowLength * Math.Sin(angleRad - arrowAngle * Math.PI / 180.0))
					);

					OpenCvSharp.Point arrowPoint2 = new OpenCvSharp.Point(
						endPoint.X - (int)(arrowLength * Math.Cos(angleRad + arrowAngle * Math.PI / 180.0)),
						endPoint.Y + (int)(arrowLength * Math.Sin(angleRad + arrowAngle * Math.PI / 180.0))
					);
						
					// draw lines
					Cv2.Line(outImage, startPoint, endPoint, color, lineThick);
					if (magnitude > 1)
					{
						Cv2.Line(outImage, endPoint, arrowPoint1, color, arrowThick);
						Cv2.Line(outImage, endPoint, arrowPoint2, color, arrowThick);
					}
				}
			}
			ResultImage.Source = OpenCvSharp.WpfExtensions.BitmapSourceConverter.ToBitmapSource(outImage);
		}

		// normalize data
		double Normalize(double value, double oldMin, double oldMax, double newMin, double newMax)
		{
			return (value - oldMin) * (double)(newMax - newMin) / (oldMax - oldMin) + newMin;
		}
	}
}