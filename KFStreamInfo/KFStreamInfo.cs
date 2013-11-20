﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using KFStreamReaderNS;

namespace KFStreamInfoNS {
	/// <summary>
	/// Example program to read KF Stream file
	/// </summary>
	class KFInfo {
		/// <summary>
		/// The main entry point
		/// </summary>
		/// <param name="args"></param>
		static void Main(string[] args) {
			string filename;	// buffer to read the file name
			bool fflag = false; // flux flag
			bool iflag = false; // index flag
			bool nflag = false; // information flag
			bool hflag = false; // histogram flag
			Index[] index;		// pointer to an Index structure
			int[] fluxes;		// pointer to a Flux array
			string info;		// pointer to the string that contains the info
			Stopwatch time = new Stopwatch();
			int count;

			Console.Write("KryoFlux Images Reader V1.1 Oct 1, 2013");
			Console.Write("\r\n");

			Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

			int argc = args.Length;
			int idx = 0;
			if (argc < 1) {
				Console.Write("KryoFlux image reader requires at least a file name");
				Console.Write("\r\n");
				Console.Write("Usage: KFInfo [-f] [-i] [-n] [-h] InputstreamFile");
				Console.Write("\r\n");
				return;
			}

			// read options flags
			while ((argc > 0) && (args[idx][0]) == '-') {
				char x = args[idx][1];
				switch (x) {
					case 'f':
						fflag = true;
						break;
					case 'i':
						iflag = true;
						break;
					case 'n':
						nflag = true;
						break;
					case 'h':
						hflag = true;
						break;
					default:
						Console.Write("invalid line option");
						Console.Write("\r\n");
						return;
				}
				argc--;
				idx++;
			} // line option(s)

			filename = args[idx];
			KFStream stream = new KFStream();

			// Read and Decode the stream file
			Console.Write("Reading and Decoding Stream: {0}\r\n", filename);
			time.Start();
			StreamStatus status = stream.readStream(filename);
			time.Stop();

			Console.Write("Return Status = {0}", status);
			if (status != StreamStatus.sdsOk) {
				Console.Write(" (Errors found while reading KryoFlux Stream file)\r\n");
				return;
			}
			else 
				Console.Write(" (No Error found)\r\n");
			Console.Write("Decode time = {0}\r\n\r\n", time.Elapsed);

			if (fflag) {
				fluxes = stream.getFluxes(); // get pointer to Index array
				count = stream.getFluxCount(); // get number of element in the array
				Console.Write("***** Flux Array Information *****");
				Console.Write("\r\n");

				for (int i = 0; i < count; i++) {
					Console.Write("Flux[{0}]={1:F5}\r\n", i,fluxes[i] * (1000000.0 / (stream.sampleClk())) );
				} // output values in the array
			} // flux transition requested

			if (iflag) {
				index = stream.getIndexes(); // get pointer to Index array
				count = stream.getIndexCount(); // get number of element in the array
				fluxes = stream.getFluxes(); // get pointer to Index array
				Console.Write("***** Index Array Information *****");
				Console.Write("\r\n");

				for (int i = 0; i < count; i++) {
					Console.Write("Index[{0}] in Flux[{1,6}]={2:F3} pre={3:F3} Elapsed={4,7:F3}\r\n",
						i, index[i].fluxIndex, fluxes[index[i].fluxIndex] * (1000000.0 / (stream.sampleClk())),
						index[i].preIndexTime * (1000000.0 / (stream.sampleClk())),
						index[i].indexTime * (1000.0 / (stream.sampleClk())) );
				}
				Console.Write("\r\n");
			} // index requested

			if (nflag) {
				info = stream.getInfoString();
				Console.Write("***** Kryoflux HW Information *****\r\n");

				// if existent display KryoFlux information - one info per line
				if (info != null) {
					count = info.Length;
					for (int i = 0; i < count; i++) {
						if (info[i] == ',') {
							Console.Write("\r\n");
							if (((i + 1) < count) && (info[i + 1] == ' '))
								i++;
						}
						else {
							Console.Write(info[i]);
						}
					}
					Console.Write("\r\n");
				}
				else {
					Console.Write("No KryFlux Information retrieved");
					Console.Write("\r\n");
				}
				Console.Write("\r\n");
			}

			if (hflag) {
				Console.Write("***** Histogram Information (non null entries) *****");
				Console.Write("\r\n");
				Statistic s = stream.getStreamStat();
				int max = s.maxFlux + 1;
				uint[] histogram = new uint[max];
				for (int i = 0; i < max; i++)
					histogram[i] = 0;
				int[] f = stream.getFluxes();
				for (int i = 0; i < stream.getFluxCount(); i++) {
					Debug.Assert(f[i] < max);
					histogram[f[i]]++;
				}

				int hist_tab = 0;
				for (int i = 0; i < max; i++) {
					if (histogram[i] > 0) {
						Console.Write("histogram[{0,4} {1,10:F3}] --> {2}\r\n", i, i * (1000000.0 / (stream.sampleClk())), histogram[i]);
						hist_tab++;
					}
				}
				Console.Write("Table entries = ");
				Console.Write(hist_tab);
				Console.Write("\r\n");
				Console.Write("\r\n");
			}

			// display some statistic
			Statistic imgStat = stream.getStreamStat();
			Console.Write("***** Statistical Information *****\r\n");
			Console.Write("USB Transfer Rate {0:F0} BPS\r\n", imgStat.avgbps);
			Console.Write("Rotation Per Minute (min/avg/max) {0:F3} / {1:F3} / {2:F3}\r\n",
				imgStat.minrpm, imgStat.avgrpm, imgStat.maxrpm);
			Console.Write("Flux transitions of {0} complete revolutions sampled\r\n", stream.getRevNumber());
			Console.Write("Total number of flux transitions recorded {0}\r\n", stream.getFluxCount());
			Console.Write("Average flux transitions per revolution {0}\r\n", imgStat.nbflux);
			Console.Write("Minimum flux transition value = {0:F5}\r\n", imgStat.minFlux * (1000000.0 / (stream.sampleClk())));
			Console.Write("Maximum flux transition value = {0:F5}\r\n", imgStat.maxFlux * (1000000.0 / (stream.sampleClk())));
			Console.Write("Sample clock = {0}\r\n", stream.sampleClk());
			Console.Write("Index clock  =  {0}\r\n", stream.indexClk());

		}
	}
}