using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Aufit {
	public enum DataType : uint { Unknown, MFM, DEC };
	public class DiskFormat {

		List<TrackDescriptor> tracks;

		public DiskFormat(TreeView trv) {
			tracks = new List<TrackDescriptor>();
			trv.ItemsSource = tracks;
			init();
		}

		public List<TrackDescriptor> init() {
			TrackDescriptor td0 = new TrackDescriptor() { Track = 0, Head = 0, BlockName = "STD_MFM_9x512"};
			TrackDescriptor td1 = new TrackDescriptor() { Track = 0, Head = 1, BlockName = "STD_MFM_9x512" };

			BlockDescriptor bd0 = new BlockDescriptor() { DataName = "ID_DATA", Sector = 1, Length = 512 };
			BlockDescriptor bd1 = new BlockDescriptor() { DataName = "ID_DATA", Sector = 2, Length = 512 };
			BlockDescriptor bd2 = new BlockDescriptor() { DataName = "ID_DATA", Sector = 3, Length = 512 };


			DataDescriptor dd0 = new DataDescriptor() { Name = "SYNC", Type = DataType.MFM, Count = 6, Value = { 0x44, 0x89, 0x44, 0x89, 0x44, 0x89 } }; 
			DataDescriptor dd1 = new DataDescriptor() { Name = "ID", Type = DataType.DEC, Count = 7, Value = {0xFE, 0x00, 0x00, 0x01, 0x02, 0xCA, 0x6F} };
			DataDescriptor dd2 = new DataDescriptor() { Name = "IGAP", Type = DataType.DEC, Count=34 };
			DataDescriptor dd3 = new DataDescriptor() { Name = "SYNC", Type = DataType.MFM, Count = 6, Value = { 0x44, 0x89, 0x44, 0x89, 0x44, 0x89 } };
			DataDescriptor dd4 = new DataDescriptor() { Name = "DATA", Type = DataType.DEC, Count = 515 };

			bd1.Items.Add(dd0);
			bd1.Items.Add(dd1);
			bd1.Items.Add(dd2);
			bd1.Items.Add(dd3);
			bd1.Items.Add(dd4);

			td0.Items.Add(bd0);
			td0.Items.Add(bd1);
			td1.Items.Add(bd0);
			td1.Items.Add(bd1);
			td1.Items.Add(bd2);

			tracks.Add(td0);
			tracks.Add(td1);
			//trv.ItemsSource = tracks;
			return tracks;
		}
	}

	public class TrackDescriptor {
		public TrackDescriptor() {
			this.Items = new ObservableCollection<BlockDescriptor>();
		}

		public ObservableCollection<BlockDescriptor> Items { get; set; }

		public int Track { get; set; }
		public int Head { get; set; }
		public string BlockName { get; set; }

		public string Content {
			get { return String.Format("T{0:D2}.{1:D1} Block=9 Density=Auto Size=6250 (Data=xxx + Gap=yyy Start=92635 -- {2}", Track, Head, BlockName); }
		}

	}

	public class BlockDescriptor {
		public BlockDescriptor() {
			this.Items = new ObservableCollection<DataDescriptor>();
		}

		public int Sector { get; set; }
		public int Length { get; set; }
		public string DataName { get; set; }
		public ObservableCollection<DataDescriptor> Items { get; set; }
		public string Content {
			get { return String.Format("{0} Sector={1} Length={2}", DataName, Sector, Length); }
		}
	}

	public class DataDescriptor {
		public DataDescriptor() {
			Value = new List<byte>();
		}
		public string Name { get; set; }
		public DataType Type { get; set; }
		public int Count { get; set; }
		public List<byte> Value { get; set; }

		public string Content {
			get {
				string s = String.Format("{0} Type={1} Count={2}{3}", Name, Type, Count, (Value.Count > 0) ? " Value " : "");
				foreach (byte b in Value) s += b.ToString("X2");
				return s;
			}
		}

	}

	public class DataContent {

	}
}
