/*!
@file FluxEnsemble.cs
<summary>Custom Framework to draw fluxes (performance)</summary>

<div class="jlg">Copyright (C) 2013-2015 Jean Louis-Guerin\n\n
This file is part of the Atari Universal FD Image Tool project.\n\n
The Atari Universal FD Image Tool project may be used and distributed without restriction provided
that this copyright statement is not removed from the file and that any
derivative work contains the original copyright notice and the associated
disclaimer.\n\n
The Atari Universal FD Image Tool project is free software; you can redistribute it
and/or modify  it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 3
of the License, or (at your option) any later version.\n\n
The Atari Universal FD Image Tool project is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.\n
See the GNU General Public License for more details.\n\n
You should have received a copy of the GNU General Public License
along with the Atari Universal FD Image Tool project; if not, write to the Free Software
Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA\n</div>

@author Jean Louis-Guerin
	
*/
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;


namespace ChartPackage {
	/// <summary>
	/// Framework elements that draws the Flux ensemble
	/// </summary>
	public class FluxEnsemble : FrameworkElement {
		/// <summary>
		/// Structure that contains information for the flux point to draw
		/// </summary>
		public struct FluxPoint {
			/// <summary>
			/// The ellipse to draw
			/// </summary>
			public EllipseGeometry ellipseGeometry;
			/// <summary>
			/// Index of the color used to draw the ellipse
			/// </summary>
			public int colorIndex;
		}


		private DrawingGroup _drawingGroup;
		private TranslateTransform _tt = new TranslateTransform(0, 0);

		/// <summary>
		/// Add all the flux to a GeometryDrawing
		/// </summary>
		/// <param name="fluxElipses">Array of FluxPoints</param>
		public FluxEnsemble(FluxPoint[] fluxElipses) {
			_drawingGroup = new DrawingGroup();

			for (int i = 0; i < fluxElipses.Length; i++) {
				EllipseGeometry eg = fluxElipses[i].ellipseGeometry;
				Brush color = Brushes.Black;

				switch (fluxElipses[i].colorIndex) {
					case 0: color = Brushes.Blue; break;
					case 1: color = Brushes.Green; break;
					case 2: color = Brushes.Red; break;
					case 3: color = Brushes.Black; break;
				}

				//switch (fluxElipses[i].colorIndex) {
				//	case 0: color = new SolidColorBrush(Colors.Blue); break;
				//	case 1: color = new SolidColorBrush(Colors.Red); break;
				//	case 2: color = new SolidColorBrush(Colors.Green); break;
				//	case 3: color = new SolidColorBrush(Colors.Yellow); break;
				//	default: color = new SolidColorBrush(Colors.Black); break;
				//}
				//color.Opacity = 0.5;

				color.Freeze();
				GeometryDrawing gd = new GeometryDrawing(color, null, eg);
				_drawingGroup.Children.Add(gd);
			}
			_drawingGroup.Transform = _tt;

		}

		/// <summary>
		/// Allow to translate the drawing (used for panning)
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		public void translateFluxes(double x, double y) {
			_tt.X = x;
			_tt.Y = y;
		}

		/// <summary>
		/// Render the drawing
		/// </summary>
		/// <param name="drawingContext"></param>
		protected override void OnRender(DrawingContext drawingContext) {
			base.OnRender(drawingContext);
			drawingContext.DrawDrawing(_drawingGroup);
		}
	}

}
