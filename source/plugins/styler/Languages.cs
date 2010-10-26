// Copyright (C) 2008-2010 Jesse Jones
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

using Gear;
using MCocoa;
using MObjc;
using MObjc.Helpers;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;

namespace Styler
{
	internal static class Languages
	{
		static Languages()
		{
			DoInit();
		}
		
		public static Language FindByExtension(string fileName)
		{
			Contract.Requires(!string.IsNullOrEmpty(fileName), "fileName is null or empty");
			
			foreach (KeyValuePair<string, string> entry in ms_globs)
			{
				if (Glob.Match(entry.Key, fileName))
				{
					Language result;
					Gear.Helpers.Unused.Value = ms_languages.TryGetValue(entry.Value, out result);
					return result;		// this may be null if the xml files aren't in sync
				}
			}
			
			return null;
		}
		
		public static Language FindByFriendlyName(string name)
		{
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			
			Language result;
			if (!ms_languages.TryGetValue(name, out result))
				throw new ArgumentException("Couldn't find language " + name);
			
			return result;
		}
		
		public static Language FindByShebang(string bang)
		{
			Contract.Requires(!string.IsNullOrEmpty(bang), "bang is null or empty");
			
			foreach (Language language in ms_languages.Values)
			{
				if (Array.IndexOf(language.Shebangs, bang) >= 0)
				{
					return language;
				}
			}
			
			return null;
		}
		
		public static IEnumerable<string> GetFriendlyNames()
		{
			foreach (string name in ms_languages.Keys)
			{
				yield return name;
			}
		}
		
		#region Private Methods
		private static void DoInit()
		{
			try
			{
				ms_dirName = "languages";
				ms_installedPath = Path.Combine(Paths.ScriptsPath, ms_dirName);
				
				ms_observer = new ObserverTrampoline(Languages.DoLoadGlobs);
				Broadcaster.Register("language globs changed", ms_observer);
				DoLoadGlobs("language globs changed", null);
				
//				DoLoadOldLanguages();
				DoLoadLanguages();
				
				// TODO: globs are saved in a pref but we never prune stale globs from the list...
				foreach (KeyValuePair<string, string> entry in ms_globs)
				{
					if (!ms_languages.ContainsKey(entry.Value))
						Log.WriteLine(TraceLevel.Info, "Startup", "glob {0} is associated with language {1}, but there is no xml file for that language", entry.Key, entry.Value);
				}
				
				foreach (string name in ms_languages.Keys)
				{
					if (!ms_globs.ContainsValue(name))
						Console.Error.WriteLine("language '{0}' is not associated with a glob", name);
				}
			}
			catch (Exception e)
			{
				Console.Error.WriteLine("Couldn't intialize languages:");
				Console.Error.WriteLine(e);
				throw;
			}
		}
		
		private static void DoLoadGlobs(string name, object v)
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			var dict = defaults.objectForKey(NSString.Create("language globs")).To<NSDictionary>();
			
			ms_globs.Clear();
			foreach (var entry in dict)
			{
				string key = entry.Key.description();
				string value = entry.Value.description();
				if (!ms_globs.ContainsKey(key))
					ms_globs.Add(key, value);
			}
		}
		
		private static void DoLoadLanguages()
		{
			string standardPath = Path.Combine(ms_installedPath, "standard");
			DoLoadLanguages(standardPath);
			
			string userPath = Path.Combine(ms_installedPath, "user");
			DoLoadLanguages(userPath);
		}
		
		private static void DoLoadLanguages(string languagesPath)
		{
			foreach (string path in Directory.GetFiles(languagesPath, "*.lang"))
			{
				try
				{
					string[] lines = File.ReadAllLines(path);
					string file = Path.GetFileName(path);
					
					var settings = new Settings();
					var elements = new List<KeyValuePair<string, string>>();		// list because we need to preserve the ordering
					for (int i = 0; i < lines.Length; ++ i)
					{
						string line = lines[i];
						
						if (line.Length > 0)
							DoProcessLine(file, line, i + 1, settings, elements);
					}
					
					if (settings.Name != null)
					{
						if (!ms_languages.ContainsKey(settings.Name))
							ms_languages[settings.Name] = new Language(path, settings, elements);
						else
							DoWriteError("Language {0} was declared in both {1} and {1}", settings.Name, path, ms_languages[settings.Name].Path);
					}
					else
					{
						DoWriteError("Missing Language setting in {0}", file);
					}
				}
				catch (Exception e)
				{
					DoWriteError("Failed to parse '{0}':", path);
					DoWriteError(e.Message);
				}
			}
		}
		
		private static void DoProcessLine(string file, string line, int lineNumber, Settings settings, List<KeyValuePair<string, string>> elements)
		{
			if (line.StartsWith("#"))
			{
				// do nothing
			}
			else if (char.IsWhiteSpace(line[0]))
			{
				if (!line.IsNullOrWhiteSpace())
					DoWriteError("{0}:{1} starts with whitespace but is not a blank line.", file, lineNumber);
			}
			else
			{
				int i = line.IndexOf(':');
				if (i > 0)
				{
					string element = line.Substring(0, i);			// note that element names can appear multiple times (for alternatives)
					string value = line.Substring(i + 1);
					int j = value.IndexOf('#');							// lines may have comments
					if (j > 0)
						value = value.Substring(0, j);
						
					if (element == "Language")
						if (settings.Name == null)
							settings.Name = value.Trim();
						else
							DoWriteError("{0} has more than one Language setting.", file);
					else if (element == "TabStops")
						if (settings.TabStops == null)
							settings.TabStops = value.Trim();
						else
							DoWriteError("{0} has more than one TabStops setting.", file);
					else
						elements.Add(new KeyValuePair<string, string>(element, value.Trim()));
				}
				else
				{
					DoWriteError("Expected a colon in {0}:{1}.", file, lineNumber);
				}
			}
		}
		
		private static void DoWriteError(string format, params object[] args)
		{
			Boss boss = ObjectModel.Create("Application");
			var transcript = boss.Get<ITranscript>();
			transcript.Show();
			transcript.WriteLine(Output.Error, format, args);
		}

#if OBSOLETE		
		private static void DoLoadOldLanguages()
		{
			// Load the schema.
			string standardPath = Path.Combine(ms_installedPath, "standard");
			string globsSchemaPath = Path.Combine(standardPath, "Language.schema");
			using (Stream stream = new FileStream(globsSchemaPath, FileMode.Open, FileAccess.Read))
			{
				XmlSchema schema = XmlSchema.Read(stream, DoValidationEvent);
				
				// Setup the xml parsing options.
				XmlReaderSettings settings = new XmlReaderSettings();
				settings.ValidationEventHandler += DoValidationEvent;
				settings.ValidationType = ValidationType.Schema;
				settings.IgnoreComments = true;
				settings.Schemas.Add(schema);
				
				// Load the xml files.
				DoLoadOldLanguages(standardPath, settings);
				
				string userPath = Path.Combine(ms_installedPath, "user");
				DoLoadOldLanguages(userPath, settings);
			}
		}
		
		// TODO: Would be nice if this would dynamically update.
		private static void DoLoadOldLanguages(string languagesPath, XmlReaderSettings settings)
		{
			// Load the xml files.
			foreach (string path in Directory.GetFiles(languagesPath, "*.xml"))
			{
				if (!path.Contains(".schema.") && !path.EndsWith("Globs.xml"))
				{
					try
					{
						using (Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read))
						{
							using (XmlReader reader = XmlReader.Create(stream, settings))
							{
								XmlDocument xml = new XmlDocument();
								xml.Load(reader);
								
								// Process the xml file.
								XmlNode node = xml.ChildNodes[0];
								string name = node.Attributes["name"].Value;
								
								if (!ms_languages.ContainsKey(name))
									ms_languages.Add(name, new Language(node));
								else
									Console.Error.WriteLine("language '{0}' was declared twice.", name);
							}
						}
					}
					catch (Exception e)
					{
						Console.Error.WriteLine("failed to parse '{0}'", path);
						Console.Error.WriteLine(e.Message);
						Console.Error.WriteLine();
					}
				}
			}
		}
		
		private static void DoValidationEvent(object sender, ValidationEventArgs e)
		{
			if (e.Severity == XmlSeverityType.Warning)
				Console.WriteLine("{0}", e.Message);
			else
				throw e.Exception;
		}
#endif
		#endregion
		
		#region Fields
		private static string ms_dirName;
		private static string ms_installedPath;
		private static ObserverTrampoline ms_observer;
		private static Dictionary<string, string> ms_globs = new Dictionary<string, string>();					// glob => language name
		private static Dictionary<string, Language> ms_languages = new Dictionary<string, Language>();	// language name => styler
		#endregion
	}
}
