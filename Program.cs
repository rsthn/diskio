using System;
using System.IO;
using System.Management;

namespace diskio
{
	class Program
	{
		protected static ManagementObject[] DRIVES;
		
		protected static string __VERSION = "2.3";

		protected static void EnumDrives()
		{
			using (ManagementObjectSearcher moQuery = new ManagementObjectSearcher ("SELECT * FROM Win32_DiskDrive"))
			{
				ManagementObjectCollection driveList = moQuery.Get();

				DRIVES = new ManagementObject[driveList.Count];
				int i = 0;

				foreach (ManagementObject disk in driveList)
				{
					DRIVES[i++] = disk;
				}
			}
		}

		protected static long GetValue (string value)
		{
			long longValue;
			
			if (long.TryParse(value, out longValue))
				return longValue;

			return 0;
		}

		protected static long ParseBytes (string str)
		{
			if (str == "0") return 0;

			long val = GetValue(str.Substring(0, str.Length-1));
			str = str.ToLower();

			switch (str.Substring(str.Length-1))
			{
				case "s": return val * 512;
				case "b": return val;
				case "k": return val * 1024;
				case "m": return val * 1024 * 1024;
				case "g": return val * 1024 * 1024 * 1024;
			}

			return GetValue(str);
		}

		protected static string FormatSize (object value)
		{
			long size = Convert.ToInt64(value);
			float rem;

			if (size < 1024)
				return size + " Bytes";

			rem = size & 1023; size /= 1024;
			if (size < 1024) return size + (rem != 0 ? (rem / 1024.0f).ToString(".00") : "") + " KB";

			rem = size & 1023; size /= 1024;
			if (size < 1024) return size + (rem != 0 ? (rem / 1024.0f).ToString(".00") : "") + " MB";

			rem = size & 1023; size /= 1024;
			if (size < 1024) return size + (rem != 0 ? (rem / 1024.0f).ToString(".00") : "") + " GB";

			rem = size & 1023; size /= 1024;
			return size + (rem != 0 ? (rem / 1024.0f).ToString(".00") : "") + " TB";
		}

		protected static void DumpDrive (int index)
		{
			if (index != -1)
			{
				ManagementObject drive = DRIVES[index];

			    Console.WriteLine("\n" + index + ": " + drive["Model"].ToString());
			    Console.WriteLine("    Name: " + drive["Name"].ToString());
			    Console.WriteLine("    Size: " + FormatSize(drive["Size"]));

			    return;
			}

			for (int i = 0; i < DRIVES.Length; i++)
			{
				ManagementObject drive = DRIVES[i];

			    Console.WriteLine("\n" + i + ": " + drive["Model"].ToString());
			    Console.WriteLine("    Name: " + drive["Name"].ToString());
			    Console.WriteLine("    Size: " + FormatSize(drive["Size"]));
			}
		}

		public static void Main(string[] args)
		{
			byte[] buffer = new byte[512];

			string driveName = null;
			string fileName = null;
			ManagementObject drive = null;

			long startingOffset = 0;
			long bytesToRead = 0;
			long numBytes = 0;

			int mode = 0;
			bool quiet = false;

			if (args.Length == 1 && args[0] == "-v")
			{
				Console.WriteLine(__VERSION);
				return;
			}

			Console.Write(
"Direct Disk I/O Utility Version "+__VERSION+" Copyright (C) 2014-2020 RedStar Technologies (rsthn.com)\n"
			);

			if (args.Length == 1 && (args[0] == "enum" || args[0] == "-enum" || args[0] == "-e"))
			{
				EnumDrives();
				DumpDrive(-1);
				return;
			}

			if (args.Length < 3)
			{
			Console.Write(
"Syntax: diskio [options] <driveName|driveNumber> fileName\n"+
"\n"+
"Options:\n"+
//                .
"   -[r]ead n     Reads the specified amount of bytes from the drive.\n" +
"   -[w]rite      Writes all data from the input file to the drive.\n" +
"   -[s]tart n    Sets the starting offset of the drive for reading/writing (in bytes).\n" +
"   -[e]num       Enumerates all available physical drives.\n" +
"   -q            Turn off the prompt for confirmation of data write.\n\n" +
"Examples:\n" +
"    diskio -s 6s -r 2m X: output            Reads 2 MB from X: starting at sector 6.\n" +
"    diskio -w Z: boot.bin                   Writes boot.bin to drive Z:\n" +
"    diskio -w 0 disk_image.img              Writes disk_image.img to enumerated drive 0.\n" +
"\n"
			);

				return;
			}

			EnumDrives();

			driveName = args[args.Length-2];
			fileName = args[args.Length-1];

			int driveNumber;

			if (int.TryParse(driveName, out driveNumber) == true)
			{
				if (driveNumber < 0 || driveNumber >= DRIVES.Length)
				{
					Console.WriteLine("Error: Drive " + driveNumber + " does not exist.\n");
					return;
				}

				drive = DRIVES[driveNumber];
				driveName = drive["Name"].ToString();
			}

			for (int i = 0; i < args.Length-2; i++)
			{
				switch (args[i])
				{
					case "-r": case "-read":
						if (++i >= args.Length-2)
						{
							Console.WriteLine("Error: Parameter missing for option: -read.\n");
							return;
						}

						bytesToRead = ParseBytes(args[i]);
						mode = 1;
						break;

					case "-w": case "-write":
						mode = 2;
						break;

					case "-q":
						quiet = true;
						break;

					case "-s": case "-start":
						if (++i >= args.Length-2)
						{
							Console.WriteLine("Error: Parameter missing for option: -start.\n");
							return;
						}

						startingOffset = ParseBytes(args[i]);
						break;
				}
			}

			FileStream file = File.Open(fileName, mode == 2 ? FileMode.Open : FileMode.Create);

			DiskIO disk = new DiskIO (driveName);
			if (!disk.isOpen())
			{
				Console.WriteLine("Error: Unable to open device \""+driveName+"\".\n");
				return;
			}

			if (startingOffset != 0)
				disk.seek(startingOffset);

			Console.WriteLine("");

			long fileSize;
			string totalBytes;

			long lastTime;
			long currentTime;

			long reportThreshold = TimeSpan.TicksPerSecond / 4; 

			switch (mode)
			{
				case 2: // Write Mode
					
					if (quiet != true)
					{
						if (drive != null)
							Console.Write("WARNING: All data on [" + drive["Model"].ToString() + "] will be destroyed. Proceed (Y/N)? ");
						else
							Console.Write("WARNING: All data on [" + driveName + "] will be destroyed. Proceed (Y/N)? ");

						if (Console.ReadKey().KeyChar.ToString().ToUpper() != "Y")
						{
							Console.WriteLine("\n");
							break;
						}

						Console.WriteLine("\n");
					}

					fileSize = new FileInfo(fileName).Length;
					totalBytes = FormatSize(fileSize);

					lastTime = 0;

					while (true)
					{
						int bytesRead = file.Read(buffer, 0, buffer.Length);
						if (bytesRead == 0) break;

						int tries = 3;

					RetryWrite:
						if (disk.write(buffer, bytesRead) != bytesRead)
						{
							disk.seek(startingOffset);

							if (tries-- > 0)
								goto RetryWrite;

							Console.WriteLine("\n["+driveName+"] (Error): Unable to write "+FormatSize(bytesRead)+" at offset "+startingOffset+".\n");
							Console.WriteLine("Error: " + disk.getLastError());

							tries = -1;
						}

						numBytes += bytesRead;
						startingOffset += bytesRead;

						if (tries == -1)
							break;

						if (tries == -1)
							disk.seek(startingOffset);

						currentTime = DateTime.Now.Ticks;

						if (currentTime - lastTime >= reportThreshold)
						{
							lastTime = currentTime;
							Console.Write("\r["+driveName+"] (Writing) " + FormatSize(numBytes) + " / " + totalBytes + " - " + (100.0f*numBytes/fileSize).ToString("0.0") + "% ...     ");
						}
					}

					if (fileSize != -1)
						Console.Write("\r["+driveName+"] (Writing) " + FormatSize(numBytes) + " / " + totalBytes + " - " + (100.0f*numBytes/fileSize).ToString("0.0") + "% --- Completed.\n");

					break;

				case 1: // Read Mode
					fileSize = bytesToRead;
					totalBytes = FormatSize(bytesToRead);

					lastTime = 0;

					while (bytesToRead > 0)
					{
						int bytesRead = bytesToRead > buffer.Length ? buffer.Length : (int)bytesToRead;

						bytesRead = disk.read(buffer, bytesRead);
						if (bytesRead == 0)
						{
							Console.WriteLine("["+driveNumber+"] (Error): Unable to read data at offset "+startingOffset+".                                      \n");
							fileSize = -1;
							break;
						}

						file.Write(buffer, 0, bytesRead);

						numBytes += bytesRead;
						bytesToRead -= bytesRead;
						startingOffset += bytesRead;

						currentTime = DateTime.Now.Ticks;

						if (currentTime - lastTime >= reportThreshold)
						{
							lastTime = currentTime;
							Console.Write("\r["+driveName+"] (Reading) " + FormatSize(numBytes) + " / " + totalBytes + " - " + (100.0f*numBytes/fileSize).ToString("0.0") + "% ...     ");
						}
					}

					if (fileSize != -1)
						Console.Write("\r["+driveName+"] (Reading) " + FormatSize(numBytes) + " / " + totalBytes + " - " + (100.0f*numBytes/fileSize).ToString("0.0") + "% --- Completed.\n");

					break;
			}

			Console.WriteLine("Finished.");

			file.Close();
			disk.close();
		}
	}
}