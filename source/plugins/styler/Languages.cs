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
			
			ms_watcher = new DirectoryWatcher(ms_installedPath, TimeSpan.FromMilliseconds(250));
			ms_watcher.Changed += Languages.DoFilesChanged;
		}
		
		public static Language FindByExtension(string fileName)
		{
			Contract.Requires(!string.IsNullOrEmpty(fileName), "fileName is null or empty");
			
			Language lang = DoFindByExtension(fileName, ms_userGlobs);		// these override standard globs so we need to check them first
			if (lang == null)
				lang = DoFindByExtension(fileName, ms_stdGlobs);
			
			return lang;
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
		private static void DoFilesChanged(object sender, DirectoryWatcherEventArgs e)
		{
			Log.WriteLine(TraceLevel.Info, "App", "Updating languages");
			
			DoLoadLanguages();
			
			Broadcaster.Invoke("languages changed", null);
		}
		
		private static void DoInit()
		{
			try
			{
				ms_dirName = "languages";
				ms_installedPath = Path.Combine(Paths.ScriptsPath, ms_dirName);
				
				ms_observer = new ObserverTrampoline(Languages.DoLoadUserGlobs);
				Broadcaster.Register("language globs changed", ms_observer);
				DoLoadUserGlobs("language globs changed", null);
				
				DoLoadLanguages();
			}
			catch (Exception e)
			{
				Console.Error.WriteLine("Couldn't intialize languages:");
				Console.Error.WriteLine(e);
				throw;
			}
		}
		
		private static void DoLoadUserGlobs(string name, object v)
		{
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			var dict = defaults.objectForKey(NSString.Create("language globs2")).To<NSDictionary>();
			
			ms_userGlobs.Clear();
			foreach (var entry in dict)
			{
				string key = entry.Key.description();
				string value = entry.Value.description();
				ms_userGlobs.Add(key, value);
			}
		}
		
		private static void DoLoadLanguages()
		{
			ms_languages.Clear();
			ms_stdGlobs.Clear();
			
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
						{
							ms_languages[settings.Name] = new Language(path, settings, elements);
							DoSetStdGlobs(settings);
						}
						else
						{
							DoWriteError("Language {0} was declared in both {1} and {1}", settings.Name, path, ms_languages[settings.Name].Path);
						}
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
		
		private static void DoSetStdGlobs(Settings settings)
		{
			foreach (string glob in settings.Globs)
			{
				if (!ms_stdGlobs.ContainsKey(glob))
					ms_stdGlobs[glob] = settings.Name;
				else
					DoWriteError("Both {0} and {1} use glob {2}.", settings.Name, ms_stdGlobs[glob], glob);
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
					value = value.Replace("\\#", Constants.Replacement);
					int j = value.IndexOf('#');							// lines may have comments
					if (j > 0)
						value = value.Substring(0, j);
					value = value.Replace(Constants.Replacement, "#");
					
					if (element == "Language")
						if (settings.Name == null)
							settings.Name = value.Trim();
						else
							DoWriteError("{0} has more than one Language setting.", file);
							
					else if (element == "Globs")
						if (settings.Globs.Length == 0)
							settings.Globs = value.Split(new char[]{' '}, StringSplitOptions.RemoveEmptyEntries);
						else
							DoWriteError("{0} has more than one Globs setting.", file);
							
					else if (element == "Word")
						settings.Word += value.Trim() + "\t";
							
					else if (element == "LineComment")
						settings.LineComment = value.Trim().Replace("\\x23", "#");
							
					else if (element == "Shebangs")
						settings.Shebangs += value.Trim() + " ";
							
					else if (element == "IgnoreWhitespace")
						settings.IgnoreWhitespace = value.Trim();
							
					else if (element == "SpacesNotTabs")
						settings.SpacesNotTabs = value.Trim();
							
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

		private static Language DoFindByExtension(string fileName, Dictionary<string, string> globs)
		{
			foreach (KeyValuePair<string, string> entry in globs)
			{
				if (Glob.Match(entry.Key, fileName))
				{
					Language result;
					Gear.Helpers.Unused.Value = ms_languages.TryGetValue(entry.Value, out result);
					return result;		// this may be null if there is a bogus user pref
				}
			}
			
			return null;
		}
		#endregion
		
		#region Fields
		private static string ms_dirName;
		private static string ms_installedPath;
		private static ObserverTrampoline ms_observer;
		private static DirectoryWatcher ms_watcher;
		private static Dictionary<string, string> ms_stdGlobs = new Dictionary<string, string>();				// glob => language name
		private static Dictionary<string, string> ms_userGlobs = new Dictionary<string, string>();			// std globs are from language files, user globs are from the prefs panel
		private static Dictionary<string, Language> ms_languages = new Dictionary<string, Language>();	// language name => styler
		#endregion
	}
}
