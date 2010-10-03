// Copyright (C) 2009 Jesse Jones
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

using Gear.Helpers;
using MCocoa;
using MObjc;
using System;
using System.Collections.Generic;

namespace NantBuilder
{
	public sealed class Prefs
	{
		public Prefs(string path)
		{
			Contract.Requires(!string.IsNullOrEmpty(path), "path is null or empty");
			
			m_path = path;
			
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			
			foreach (string option in ms_valueNames)
			{
				NSString str = defaults.stringForKey(NSString.Create(m_path + option));
				if (!NSObject.IsNullOrNil(str))
					m_values.Add(option, str.description());
			}
			
			foreach (string option in ms_flagNames)
			{
				m_switches.Add(option, defaults.boolForKey(NSString.Create(m_path + option)));
			}
		}
		
		public string GetArgs()
		{
			var builder = new System.Text.StringBuilder();
			
			foreach (KeyValuePair<string, bool> entry in m_switches)
			{
				if (entry.Value)
				{
					builder.Append(entry.Key);
					builder.Append(' ');
				}
			}
			
			foreach (KeyValuePair<string, string> entry in m_values)
			{
				if (entry.Value.Length > 0)
				{
					builder.Append(entry.Key);
					builder.Append(':');
					builder.Append(entry.Value.Replace(" ", "\\ "));
					builder.Append(' ');
				}
			}
			
			return builder.ToString();
		}
		
		// Options are things like "-verbose".
		public string GetValue(string option)
		{
			Contract.Requires(!string.IsNullOrEmpty(option), "option is null or empty");
			Contract.Requires(option.StartsWith("-"), option + " does not start with a -");
			
			string value;
			if (!m_values.TryGetValue(option, out value))
				value = string.Empty;
			
			return value;
		}
		
		public void SetValue(string option, string value)
		{
			Contract.Requires(!string.IsNullOrEmpty(option), "option is null or empty");
			Contract.Requires(option.StartsWith("-"), option + " does not start with a -");
			
			m_values[option] = value;
		}
		
		public bool GetFlag(string option)
		{
			Contract.Requires(!string.IsNullOrEmpty(option), "option is null or empty");
			Contract.Requires(option.StartsWith("-"), option + " does not start with a -");
			
			bool value;
			if (!m_switches.TryGetValue(option, out value))
				value = false;
			
			return value;
		}
		
		public void SetFlag(string option, bool value)
		{
			Contract.Requires(!string.IsNullOrEmpty(option), "option is null or empty");
			Contract.Requires(option.StartsWith("-"), option + " does not start with a -");
			
			m_switches[option] = value;
		}
		
		public void Save()
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			
			foreach (KeyValuePair<string, string> entry in m_values)
			{
				defaults.setObject_forKey(NSString.Create(entry.Value), NSString.Create(m_path + entry.Key));
			}
			
			foreach (KeyValuePair<string, bool> entry in m_switches)
			{
				defaults.setBool_forKey(entry.Value, NSString.Create(m_path + entry.Key));
			}
		}
		
		#region Fields
		private string m_path;
		private Dictionary<string, string> m_values = new Dictionary<string, string>();
		private Dictionary<string, bool> m_switches = new Dictionary<string, bool>();
		
		private static string[] ms_valueNames = new string[]
		{
			"-targetframework",
			"-extension",
			"-indent",
			"-logger",
			"-logfile",
			"-listener",
		};
		private static string[] ms_flagNames = new string[]
		{
			"-quiet",
			"-verbose",
			"-debug",
		};
		#endregion
	}
}
