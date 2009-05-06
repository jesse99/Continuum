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

using Gear.Helpers;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Remoting;

namespace Shared
{
	public static class Log
	{
		static Log()
		{
			try
			{
#if TEST
				// TestFixtureSetUp methods can override this via SetLevel.
				ms_defaultLevel = TraceLevel.Info;
				
				ObjectHandle handle = Activator.CreateInstance(
					"continuum",									// assemblyName
					"Continuum.PrettyTraceListener",		// typeName
					false,											// ignoreCase
					BindingFlags.CreateInstance,			// bindingAttr,
					null,												// binder
					new object[]{"/tmp/test.log"},		// args (note that we can't rely on the working directory if we're running the tests via Continuum)
					null,												// culture
					null,												// activationAttributes
					null);											// securityInfo
				TextWriterTraceListener listener = (TextWriterTraceListener) (handle.Unwrap());
				Trace.Listeners.Add(listener);
				
				Trace.AutoFlush = true;
				Trace.IndentSize = 4;
#else
				var section = ConfigurationManager.GetSection("logger") as LoggerSection;
				if (section != null)
					DoInitCategories(section);
#endif
			}
			catch (Exception e)
			{
				Console.Error.WriteLine("Couldn't initialize the logger: {0}", e.Message);	// typically this will be a bad config file
			}
		}
		
#if TEST
		// Test because this code is dicy in the presence of threads.
		public static void SetLevel(TraceLevel level)
		{
			ms_defaultLevel = level;
		}
#endif
		
		// Writing
		[Conditional("TRACE")]
		public static void WriteLine(string category)
		{
			WriteLine(TraceLevel.Info, category, string.Empty);
		}
	
		[Conditional("TRACE")]
		public static void WriteLine(string category, string message)
		{
			WriteLine(TraceLevel.Info, category, message);
		}
	
		[Conditional("TRACE")]
		public static void WriteLine(string category, string format, params object[] args)
		{
			WriteLine(TraceLevel.Info, category, format, args);
		}
		
		[Conditional("TRACE")]
		public static void WriteStackTrace(string category)
		{
			WriteStackTrace(TraceLevel.Info, category);
		}
		
		[Conditional("TRACE")]
		public static void WriteLine(TraceLevel level, string category, string message)
		{
			if (IsEnabled(level, category))
				DoWriteLine(level, category, message);
		}
		
		[Conditional("TRACE")]
		public static void WriteLine(TraceLevel level, string category, string format, params object[] args)
		{
			if (IsEnabled(level, category))
				DoWriteLine(level, category, string.Format(format, args));
		}
		
		[Conditional("TRACE")]
		public static void WriteStackTrace(TraceLevel level, string category)
		{
			if (IsEnabled(level, category))
			{
				var stack = new StackTrace(2);
				DoWriteLine(level, category, stack.ToString());
			}
		}
		
		// Misc
		[Conditional("TRACE")]
		public static void Flush()
		{
			Trace.Flush();
		}
		
		[Conditional("TRACE")]
		public static void Indent()
		{
			Trace.Indent();
		}
		
		[Conditional("TRACE")]
		public static void Unindent()
		{
			Trace.Unindent();
		}
		
		public static bool IsEnabled(TraceLevel level, string category)
		{
			bool enabled;
			
			TraceLevel current;
			if (!ms_levels.TryGetValue(category, out current))
				current = ms_defaultLevel;
				
			enabled = level != TraceLevel.Off && level <= current;
			
			return enabled;
		}
		
		#region Private Methods
#if !TEST
		private static void DoInitCategories(LoggerSection section)
		{
			for (int i = 0; i < section.Categories.Count; ++i)
			{
				string name = section.Categories[i].Name;
				
				try
				{
					TraceLevel level = (TraceLevel) Enum.Parse(typeof(TraceLevel), section.Categories[i].Level);
					if (name != "*")
					{
						if (ms_levels.ContainsKey(name))
							Console.Error.WriteLine("Log category {0} appears twice in the config file: overwriting the older value.", name);
						
						ms_levels[name] = level;
					}
					else
						ms_defaultLevel = level;
				}
				catch (Exception e)
				{
					Console.Error.WriteLine("Failed to add log category {0}.", name);
					Console.Error.WriteLine(e.Message);		// typically this will be an Enum.Parse error
				}
			}
		}
#endif
		
		private static void DoWriteLine(TraceLevel level, string category, string mesg)
		{
			Trace.WriteLine(mesg, category + " " + level);	// note that Trace is thread safe
		}
		#endregion
		
		#region Private Types
		private sealed class LoggerSection : ConfigurationSection
		{
			[ConfigurationProperty("categories", IsDefaultCollection = false)]
			[ConfigurationCollection(typeof(CategoriesCollection))]
			public CategoriesCollection Categories
			{
				get {return (CategoriesCollection) base["categories"];}
			}
		}
		
		[ConfigurationCollection(typeof(CategoryElement))]
		private sealed class CategoriesCollection : ConfigurationElementCollection
		{
			protected override ConfigurationElement CreateNewElement()
			{
				return new CategoryElement();
			}
			
			protected override object GetElementKey(ConfigurationElement element)
			{
				return ((CategoryElement) element).Name;
			}
			
			public CategoryElement this[int index]
			{
				get {return (CategoryElement) BaseGet(index);}
			}
		}
		
		private sealed class CategoryElement : ConfigurationElement
		{
			[ConfigurationProperty("name", DefaultValue = "", IsKey = true, IsRequired = true)]
			public string Name
			{
				get {return (string) this["name"];}
			}
			
			[ConfigurationProperty("level", DefaultValue = "Warning", IsKey = false, IsRequired = true)]
			public string Level
			{
				get {return (string) this["level"];}
			}
		}
		#endregion
		
		#region Fields
		private static TraceLevel ms_defaultLevel = TraceLevel.Warning;
		private static Dictionary<string, TraceLevel> ms_levels = new Dictionary<string, TraceLevel>();
		#endregion
	}
}
