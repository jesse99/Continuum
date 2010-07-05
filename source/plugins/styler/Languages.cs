// Copyright (C) 2008-2009 Jesse Jones
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
using System.Xml;
using System.Xml.Schema;

namespace Styler
{
	internal static class Languages
	{
		public static Language FindByExtension(string fileName)
		{
			Contract.Requires(!string.IsNullOrEmpty(fileName), "fileName is null or empty");
			
			if (!ms_inited)
				DoInit();
				
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
			
			if (!ms_inited)
				DoInit();
				
			Language result;
			if (!ms_languages.TryGetValue(name, out result))
				throw new ArgumentException("Couldn't find language " + name);
			
			return result;
		}
		
		public static Language FindByShebang(string bang)
		{
			Contract.Requires(!string.IsNullOrEmpty(bang), "bang is null or empty");
			
			if (!ms_inited)
				DoInit();
				
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
			if (!ms_inited)
				DoInit();
				
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
			
			ms_inited = true;
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
				DoLoadLanguages(standardPath, settings);
				
				string userPath = Path.Combine(ms_installedPath, "user");
				DoLoadLanguages(userPath, settings);
			}
		}
		
		// TODO: Would be nice if this would dynamically update.
		private static void DoLoadLanguages(string languagesPath, XmlReaderSettings settings)
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
		#endregion
		
		#region Fields
		private static string ms_dirName;
		private static string ms_installedPath;
		private static bool ms_inited;
		private static ObserverTrampoline ms_observer;
		private static Dictionary<string, string> ms_globs = new Dictionary<string, string>();					// glob => language name
		private static Dictionary<string, Language> ms_languages = new Dictionary<string, Language>();	// language name => styler
		#endregion
	}
}
