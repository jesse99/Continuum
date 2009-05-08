// Copyright (C) 2008 Jesse Jones
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using MCocoa;
using MObjc;
using MObjc.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Shared
{
	[Serializable]
	public sealed class DirectoryWatcherEventArgs : EventArgs
	{
		public DirectoryWatcherEventArgs(string[] paths)
		{
			Paths = paths;
		}
		
		public string[] Paths {get; private set;}
	}
	
	// System.IO.FileSystemWatcher doesn't seem to work too well on OS X. There
	// is a managed implementation which is supposed to work better (enabled via
	// an environment variable), but it has a fair amount of overhead because it
	// relies on polling.
	// /System/Library/Frameworks/CoreServices.framework/Versions/A/Frameworks/CarbonCore.framework/Versions/A/Headers/FSEvents.h
	public sealed class DirectoryWatcher : IDisposable
	{
		[ThreadModel("finalizer")]
		~DirectoryWatcher()
		{
			Dispose(false);
		}
		
		public DirectoryWatcher(string path, TimeSpan latency)
		{
			Log.WriteLine(TraceLevel.Verbose, "App", "creating a directory watcher for '{0}'", path);
			
			Path = path;
			m_callback = this.DoCallback;
			
			NSArray paths = NSArray.Create(path);
			
			m_stream = FSEventStreamCreate(		// note that the stream will always be valid
				IntPtr.Zero,									// allocator
				m_callback, 									// callback
				IntPtr.Zero,							 		// context
				paths,											// pathsToWatch
				kFSEventStreamEventIdSinceNow, 	// sinceWhen
				latency.TotalSeconds,						// latency (in seconds)
				FSEventStreamCreateFlags.kFSEventStreamCreateFlagNone);	// flags
				
			FSEventStreamScheduleWithRunLoop(
				m_stream,							// streamRef
				CFRunLoopGetMain(),			// runLoop
				kCFRunLoopDefaultMode);	// runLoopMode
				
			bool started = FSEventStreamStart(m_stream);
			if (!started)
			{
				GC.SuppressFinalize(this);
				throw new InvalidOperationException("Failed to start FSEvent stream for " + path);
			}
			
			ActiveObjects.Add(this);
			Log.WriteLine(TraceLevel.Verbose, "App", "created the directory watcher");
		}
		
		// This will fire if files are added, removed, or changed from the specified
		// directory or any of its sub-directories.
		[ThreadModel(ThreadModel.MainThread)]
		public event EventHandler<DirectoryWatcherEventArgs> Changed;
		
		public string Path {get; private set;}
		
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		
		#region Private Members
		[ThreadModel(ThreadModel.SingleThread)]
		private void Dispose(bool disposing)
		{
			if (m_stream != IntPtr.Zero)
			{
				FSEventStreamStop(m_stream);
				FSEventStreamInvalidate(m_stream);
				FSEventStreamRelease(m_stream);
				
				m_stream = IntPtr.Zero;
			}
		}
		
		// TODO: this is called even if a file is simply touched. AFAICT there is no way
		// to filter these out (eventPaths would be a candidate, but it seems to always 
		// be a directory). 
		// Note that this is called from the run loop so it is not threaded.
		private void DoCallback(IntPtr streamRef, IntPtr clientCallBackInfo, int numEvents, IntPtr eventPaths, IntPtr eventFlags, IntPtr eventIds)
		{	
			Contract.Assert(System.Threading.Thread.CurrentThread.ManagedThreadId == 1, "can only be used from the main thread");
			int bytes = Marshal.SizeOf(typeof(IntPtr));
			
			string[] paths = new string[numEvents];
			for (int i = 0; i < numEvents; ++i)
			{
				IntPtr p = Marshal.ReadIntPtr(eventPaths, i*bytes);
				paths[i] = Marshal.PtrToStringAuto(p);
			}
			
			var handler = Changed;
			if (handler != null)
				handler(this, new DirectoryWatcherEventArgs(paths));
				
			GC.KeepAlive(this);
		}
		
		[Flags]
		[Serializable]
		private enum FSEventStreamCreateFlags : uint
		{
			kFSEventStreamCreateFlagNone  = 0x00000000,
			kFSEventStreamCreateFlagUseCFTypes = 0x00000001,
			kFSEventStreamCreateFlagNoDefer = 0x00000002,
			kFSEventStreamCreateFlagWatchRoot = 0x00000004,
		}
		
		private static readonly IntPtr kCFRunLoopDefaultMode = NSString.Create("kCFRunLoopDefaultMode").Retain();
		private ulong kFSEventStreamEventIdSinceNow = 0xFFFFFFFFFFFFFFFFUL;
		
		private delegate void FSEventStreamCallback(
			IntPtr streamRef,
			IntPtr clientCallBackInfo,
			int numEvents,
			IntPtr eventPaths,
			IntPtr eventFlags,
			IntPtr eventIds);
		#endregion
		
		#region P/Invokes
		[DllImport("/System/Library/Frameworks/CoreServices.framework/CoreServices")]
		private extern static IntPtr CFRunLoopGetMain();
		
		[DllImport("/System/Library/Frameworks/CoreServices.framework/CoreServices")]
		private extern static IntPtr FSEventStreamCreate(
			IntPtr allocator,
			FSEventStreamCallback callback,
			IntPtr context,
			IntPtr pathsToWatch,
			ulong sinceWhen,
			double latency,
			FSEventStreamCreateFlags flags);
		
		[DllImport("/System/Library/Frameworks/CoreServices.framework/CoreServices")]
		private extern static void FSEventStreamScheduleWithRunLoop(
			IntPtr   streamRef,
			IntPtr   runLoop,
			IntPtr   runLoopMode);
		
		[DllImport("/System/Library/Frameworks/CoreServices.framework/CoreServices")]
		[return: MarshalAs(UnmanagedType.U1)] 
		private extern static bool FSEventStreamStart(
			IntPtr   streamRef);
		
		[DllImport("/System/Library/Frameworks/CoreServices.framework/CoreServices")]
		[ThreadModel(ThreadModel.SingleThread)]
		private extern static void FSEventStreamStop(
			IntPtr   streamRef);
		
		[DllImport("/System/Library/Frameworks/CoreServices.framework/CoreServices")]
		[ThreadModel(ThreadModel.SingleThread)]
		private extern static void FSEventStreamInvalidate(
			IntPtr   streamRef);
		
		[DllImport("/System/Library/Frameworks/CoreServices.framework/CoreServices")]
		[ThreadModel(ThreadModel.SingleThread)]
		private extern static void FSEventStreamRelease(
			IntPtr   streamRef);
		#endregion
		
		#region Fields		
		private IntPtr m_stream;
		private FSEventStreamCallback m_callback;	// need to keep a reference around so that th	is isn't GCed
		#endregion
	}
}
