
using System;
using System.Diagnostics;
using System.Collections;
using System.Runtime.InteropServices;

namespace diskio
{
	/**
	**	Direct sector-based Disk I/O interface.
	*/
	public class DiskIO
	{
		/**
		**	DLL imports from KERNEL32.dll
		*/
		[DllImport("kernel32.dll")]
		public static extern uint GetLastError();

		[DllImport("kernel32.dll", SetLastError = true)]
		private extern static int CloseHandle (IntPtr hObject);

		[DllImport("kernel32.dll", SetLastError = true)]
		private extern static bool WriteFile (IntPtr handle, byte[] buffer, UInt16 count, out UInt32 written, IntPtr lpOverlapped);

		[DllImport("kernel32.dll", SetLastError = true)]
		private extern static bool ReadFile (IntPtr handle, byte[] buffer, UInt16 toRead, ref UInt32 read, IntPtr lpOverLapped);

		[DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		private extern static uint SetFilePointer (IntPtr hFile, Int32 lDistanceToMove, IntPtr lpDistanceToMoveHigh, UInt32 tdwMoveMethod);

		[DllImport("kernel32.dll", SetLastError = true)]
		private extern static IntPtr CreateFile(string lpFileName, UInt32 dwDesiredAccess, UInt32 dwShareMode, IntPtr lpSecurityAttributes, UInt32 dwCreationDisposition, UInt32 dwFlagsAndAttributes, IntPtr hTemplateFile);

		/**
		**	Handle for the currently active device, set automatically when the device is opened. Can be
		**	accessed using the getDeviceHandle method.
		*/
		private IntPtr hDevice;

		/**
		**	Constructs the I/O interface for the specified device name.
		*/
		public DiskIO (String deviceName)
		{
			hDevice = CreateFile ((!deviceName.StartsWith(@"\\.\") ? @"\\.\" : "") + deviceName, 0xC0000000 /* GENERIC_READ | GENERIC_WRITE */, 0 /* NO_SHARE */, IntPtr.Zero, 0x03 /* OPEN_EXISTING */, 0, IntPtr.Zero);
		}

		/**
		**	Returns the windows device handle of the active device.
		*/
		public IntPtr getDeviceHandle ()
		{
			return hDevice;
		}

		/**
		**	Returns a boolean indicating if the device is open or not.
		*/
		public bool isOpen ()
		{
			return (int)hDevice == -1 ? false : true;
		}

		/**
		**	Closes the device, any subsequent reading/writing operation will fail.
		*/
		public void close ()
		{
			if ((int)hDevice == -1)
				return;

			CloseHandle (hDevice);
			hDevice = new IntPtr(-1);
		}

		/**
		**	Moves the internal file pointer to the specified offset.
		*/
		public bool seek (long offset)
		{
			if (!isOpen ()) return false;

			SetFilePointer (hDevice, (Int32)(offset & 0xFFFFFFFF), new IntPtr((Int32)(offset >> 32)), 0 /* SEEK_SET */);

			return true;
		}

		/**
		**	Writes data to the device from the specified buffer at the current file pointer.
		*/
		public int write (byte[] buffer, int length)
		{
			if (!isOpen ()) return 0;

			UInt32 bytesWritten = 0;

			WriteFile (hDevice, buffer, (ushort)length, out bytesWritten, IntPtr.Zero);
			return (int)bytesWritten;
		}

		/**
		**	Reads a data sector from the device (at the current file pointer) into the specified buffer.
		*/
		public int read (byte[] buffer, int length)
		{
			if (!isOpen ()) return 0;

			UInt32 bytesRead = 0;

			ReadFile (hDevice, buffer, (ushort)length, ref bytesRead, IntPtr.Zero);
			return (int)bytesRead;
		}

		/*
		**	Returns the last error found.
		*/
		public string getLastError()
		{
			return WinErrors.GetSystemMessage(Marshal.GetLastWin32Error());
		}
	}
};
