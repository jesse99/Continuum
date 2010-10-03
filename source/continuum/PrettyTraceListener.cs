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

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Continuum
{
	// Like TextWriterTraceListener except:
	// 1) We truncate the old log file instead of appending.
	// 2) We align the category column.
	// 3) We support the Timestamp and ThreadId TraceOptions in writes.
	internal sealed class PrettyTraceListener : TextWriterTraceListener
	{
		public PrettyTraceListener(string path) : base(DoGetStream(path), string.Empty)
		{
			m_path = path;
		}
		
		// This is the only Write method our logger calls.
		public override void WriteLine(string message, string category)
		{
			if (m_disposed)
				throw new ObjectDisposedException(GetType().Name);
			
			if (message.Length == 0)
			{
				WriteLine(string.Empty);
			}
			else if (message.IndexOfAny(m_eolChars) >= 0)
			{
				string[] lines = message.Split('\r', '\n');
				DoWrite(lines[0], category);
				for (int i = 1; i < lines.Length; ++i)
					DoWrite(lines[i], string.Empty);
			}
			else
				DoWrite(message, category);
		}
		
		public override void Fail(string message, string detailMessage)
		{
			if (m_disposed)
				throw new ObjectDisposedException(GetType().Name);
				
			var stack = new StackTrace(5);
				
			if (Trace.Listeners.Count == 1 && m_path != "stdout" && m_path != "stderr")
			{
				// We only do this if we are the only listener because if there are
				// multiple listeners one is usually a console listener.
				Console.Error.WriteLine("Assert: " + message);
				if (!string.IsNullOrEmpty(detailMessage))
					Console.Error.WriteLine(detailMessage);
				Console.Error.WriteLine("{0}", stack);
			}
			
			DoWrite("Assert: " + message, string.Empty);
			if (!string.IsNullOrEmpty(detailMessage))
				DoWrite(detailMessage, string.Empty);
			DoWrite(stack.ToString(), string.Empty);
			
			Flush();
		}
		
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			m_disposed = true;
		}
		
		#region Private Methods
		private static Stream DoGetStream(string path)
		{
			if (path == "stdout")
				return Console.OpenStandardOutput();
			
			else if (path == "stderr")
				return Console.OpenStandardError();
			
			else
				return DoCreateFileStream(path);
		}
		
		private static Stream DoCreateFileStream(string path)
		{
			Stream stream;
			
			try
			{
				stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
			}
			catch (Exception e)	// TODO: this pretty much sucks: we should catch a specific exception so things like ContractExceptions don't slip through the cracks
			{
				// We don't want to just roll over and die if the app is run on a machine that
				// doesn't have the log path used by the machine it was built for.
				Console.Error.WriteLine("Couldn't create a log for '{0}'. Using stdout instead.", path);
				Console.Error.WriteLine(e.Message);
				stream = Console.OpenStandardOutput();
			}
			
			return stream;
		}
		
		private void DoWrite(string message, string category)
		{
			if (category.Length > 0)
			{
				if ((TraceOutputOptions & TraceOptions.ThreadId) == TraceOptions.ThreadId)
				{
					string name = Thread.CurrentThread.Name;
					
					if (!string.IsNullOrEmpty(name))
						category = name + " " + category;
					else if (Thread.CurrentThread == m_mainThread)
						category = "main " + category;
					else
						category = Thread.CurrentThread.ManagedThreadId + " " + category;
				}
				
				if ((TraceOutputOptions & TraceOptions.Timestamp) == TraceOptions.Timestamp)
				{
					TimeSpan span = DateTime.Now - m_startTime;
					string time = string.Format("{0:00}:{1:00}:{2:00}.{3:0.000} ", span.Hours, span.Minutes, span.Seconds, span.Milliseconds/1000.0);
					category = time + " " + category;
				}
				
				m_categoryWidth = Math.Max(category.Length + 2, m_categoryWidth);
				Write(category);
			}
			
			Write(new string(' ', m_categoryWidth - category.Length));
			WriteLine(message);
		}
		#endregion
		
		#region Fields
		private string m_path;
		private Thread m_mainThread = Thread.CurrentThread;
		private DateTime m_startTime = DateTime.Now;
		private int m_categoryWidth;
		private char[] m_eolChars = new char[]{'\r', '\n'};
		private bool m_disposed;
		#endregion
	}
}
