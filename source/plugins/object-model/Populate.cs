// Copyright (C) 2007-2008 Jesse Jones
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
using Mono.Cecil;
using Mono.Cecil.Binary;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace ObjectModel
{
	internal sealed class Populate : IOpened, IShutdown, IObserver
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public static string GetDatabasePath(Boss boss)
		{
			var editor = boss.Get<IDirectoryEditor>();
			string name = Path.GetFileName(editor.Path);
			
			string path = Paths.GetAssemblyDatabase(name);
			
			return path;
		}
		
		public void Opened()
		{
			// Putting the databases into each directory is a little bit wasteful of disk
			// space because a lot of assemblies will be shared across projects (especially
			// the system assemblies). But there are also advantages:
			// 1) It's much faster to do database lookups if the database doesn't contain
			// lot's of assemblies which are not used by the current project.
			// 2) Lookups automatically return only the results which pertain to the current
			// project. With a single database we'd have to do this filtering manually to get.
			// the best results which is a bit painful.
			// 3) Assembly versioning is a bit nicer because the queries run against the
			// assemblies which are being used instead of the latest version of the assembly.
			m_path = Populate.GetDatabasePath(m_boss);
			Log.WriteLine("ObjectModel", "'{0}' was opened", m_path);
			
			Broadcaster.Register("opened directory", this);
			Broadcaster.Register("closing directory", this);
			
			Log.WriteLine(TraceLevel.Verbose, "ObjectModel", "starting thread for {0}", m_path);
			m_thread = new Thread(() => DoParseAssemblies(m_path));
			m_thread.Name = "parse assemblies";
			m_thread.IsBackground = false;
			m_thread.Priority = ThreadPriority.BelowNormal;		// this is ignored on Mono 2.0
			m_thread.Start();
//	Console.WriteLine("{0} is using thread {1}", Path.GetFileName(path), m_thread.ManagedThreadId);
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "opened directory":
					DoOnOpenDir(name, value);
					break;
					
				case "closing directory":
					OnShutdown();
					break;
					
				default:
					Trace.Fail("bad name: " + name);
					break;
			}
		}
		
		// Cleanly kill our threads so that they don't die as they are updating
		// the database.
		public void OnShutdown()
		{
			lock (m_lock)
			{
				m_running = false;
				Monitor.PulseAll(m_lock);
			}
		}
		
		#region Private Methods
		private void DoOnOpenDir(string name, object value)
		{
			Boss boss = (Boss) value;
			if (boss == m_boss)
			{
				var editor = boss.Get<IDirectoryEditor>();
				
				if (!m_watchers.Any(w => Paths.AreEqual(w.Path, editor.Path)))
				{
					var watcher = new DirectoryWatcher(editor.Path, TimeSpan.FromMilliseconds(100));
					watcher.Changed += this.DoDirChanged;
					
					m_watchers.Add(watcher);
				}
				
				DoAddFiles(editor.Path);
			}
		}
		
		private void DoDirChanged(object o, DirectoryWatcherEventArgs e)
		{
			foreach (string path in e.Paths)
				DoAddFiles(path);
		}
		
		private void DoAddFiles(string root)
		{
			try
			{
				if (!root.Contains(".app/") && !root.Contains("/.svn") && Directory.Exists(root))
				{
					Boss boss = Gear.ObjectModel.Create("FileSystem");
					var fs = boss.Get<IFileSystem>();
					
					string[] dlls = fs.GetAllFiles(root, "*.dll");
					string[] exes = fs.GetAllFiles(root, "*.exe");
					
					lock (m_lock)
					{
						m_files.AddRange(dlls);
						m_files.AddRange(exes);
//			foreach (string f in dlls)
//				Console.WriteLine("adding file {0} for thread {1}", f, m_thread.ManagedThreadId);
//			foreach (string f in exes)
//				Console.WriteLine("adding file {0} for thread {1}", f, m_thread.ManagedThreadId);

						Monitor.PulseAll(m_lock);
					}
				}
			}
			catch (IOException e)
			{
				Log.WriteLine(TraceLevel.Info, "Errors", "error checking '{0}' for assemblies:", root);
				Log.WriteLine(TraceLevel.Info, "Errors", e.Message);
			}
		}
		
		private void DoParseAssemblies(string path)	// threaded
		{
			m_database = new Database(path, "Populate-" + Path.GetFileNameWithoutExtension(path));			
			DoCreateTables();
			
			while (true)
			{
				try
				{
					DoDeleteAssemblies();
					Log.WriteLine(TraceLevel.Verbose, "ObjectModel", "pruned assemblies");
					
					string root;
					lock (m_lock)
					{
						while (m_files.Count == 0 && m_running)
						{
//	Console.WriteLine("thread {0} is blocking", Thread.CurrentThread.ManagedThreadId);
							Log.WriteLine(TraceLevel.Info, "ObjectModel", "waiting");
							Unused.Value = Monitor.Wait(m_lock);
						}
						
						if (!m_running)
							break;
						
						root = m_files[0];
						m_files.RemoveAt(0);
					}
					
					if (!root.Contains(".app/") && !root.Contains("/.svn"))
					{
						DoUpdateAssemblies(root);
						Log.WriteLine(TraceLevel.Verbose, "ObjectModel", "updated assemblies for '{0}'", root);
					}
				}
				catch (Exception e)
				{
					Console.Error.WriteLine("{0}", e);
					throw;			// TODO: trapping the exception ties up lots of cpu as the thread tries to redo the op
				}
			}
//	Console.WriteLine("thread {0} is exiting", Thread.CurrentThread.ManagedThreadId);

			Log.WriteLine(TraceLevel.Verbose, "ObjectModel", "exiting thread for {0}", m_path);
		}
		
		// The database sizes are a lot larger than I would like, but it's not clear
		// how they can be reduced. I did experiment with using a Names table
		// and storing integers instead of strings for everything but the display_text
		// field, but that only reduced the db size by about 30% and makes the
		// auto-complete queries rather painful because they want access to names
		// for the parse cache.
		private void DoCreateTables()		// threaded
		{
			// We could place an upper limit on some of the field sizes, but it won't
			// do much good because sqlite stores just what is needed.
			Log.WriteLine(TraceLevel.Verbose, "ObjectModel", "creating tables");
			m_database.Update("create tables", () =>
			{
				// TODO: once sqlite supports it the hash foreign keys should use ON DELETE CASCADE
				m_database.Update(@"
					CREATE TABLE IF NOT EXISTS Assemblies(
						path TEXT NOT NULL PRIMARY KEY
							CONSTRAINT absolute_path CHECK(substr(path, 1, 1) = '/'),
						name TEXT NOT NULL
							CONSTRAINT no_empty_name CHECK(length(name) > 0),
						culture TEXT NOT NULL
							CONSTRAINT no_empty_culture CHECK(length(culture) > 0),
						version TEXT NOT NULL,
						write_time INTEGER NOT NULL
							CONSTRAINT sane_time CHECK(write_time > 0),
						assembly INTEGER NOT NULL
							CONSTRAINT no_zero_assembly CHECK(assembly != 0)
					)");

				m_database.Update(@"
					CREATE TABLE IF NOT EXISTS Types(
						root_name TEXT PRIMARY KEY NOT NULL 
							CONSTRAINT no_empty_root CHECK(length(root_name) > 0),
						assembly INTEGER NOT NULL 
							REFERENCES Assemblies(assembly),
						namespace TEXT NOT NULL,
						name TEXT NOT NULL 
							CONSTRAINT no_empty_name CHECK(length(name) > 0),
						base_type_name TEXT NOT NULL,
						interface_type_names TEXT NOT NULL
							CONSTRAINT valid_interfaces CHECK(length(interface_type_names) = 0 OR substr(interface_type_names, -1) = ':'),
						generic_arg_count INTEGER NOT NULL
							CONSTRAINT non_negative_arg_count CHECK(generic_arg_count >= 0),
						visibility INTEGER NOT NULL
							CONSTRAINT valid_vis CHECK(visibility >= 0 AND visibility <= 3),
						attributes INTEGER NOT NULL
							CONSTRAINT valid_attributes CHECK(attributes >= 0 AND attributes < 4*16)
					)");
				
				m_database.Update(@"
					CREATE TABLE IF NOT EXISTS SpecialTypes(
						special_name TEXT PRIMARY KEY NOT NULL 
							CONSTRAINT no_empty_name CHECK(length(special_name) > 0),
						element_type_name TEXT NOT NULL,
						rank INTEGER NOT NULL 
							CONSTRAINT non_negative_rank CHECK(rank >= 0),
						generic_type_names TEXT NOT NULL
							CONSTRAINT valid_generics CHECK(length(generic_type_names) = 0 OR substr(generic_type_names, -1) = ':'),
						kind INTEGER NOT NULL
							CONSTRAINT valid_kind CHECK(kind >= 0 AND kind <= 3)
					)");
				
				m_database.Update(@"
					CREATE TABLE IF NOT EXISTS Methods(
						display_text TEXT NOT NULL PRIMARY KEY
							CONSTRAINT valid_text CHECK(length(display_text) >= 4),
						name TEXT NOT NULL 
							CONSTRAINT no_empty_name CHECK(length(name) > 0),
						return_type_name TEXT NOT NULL 
							CONSTRAINT no_empty_return CHECK(length(return_type_name) > 0),
						declaring_root_name TEXT NOT NULL 
							REFERENCES Types(root_name),
						params_count INTEGER NOT NULL
							CONSTRAINT non_negative_params_count CHECK(params_count >= 0),
						generic_arg_count INTEGER NOT NULL
							CONSTRAINT non_negative_generic_count CHECK(generic_arg_count >= 0),
						assembly INTEGER NOT NULL 
							REFERENCES Assemblies(assembly),
						extend_type_name TEXT NOT NULL,
						access INTEGER NOT NULL,
						static INTEGER NOT NULL
							CONSTRAINT valid_static CHECK(static >= 0 AND static <= 1),
						file_path TEXT NOT NULL,
						line INTEGER NOT NULL
							CONSTRAINT valid_line CHECK(line >= -1),
						kind INTEGER NOT NULL
							CONSTRAINT valid_kind CHECK(kind >= 0 AND kind <= 8)
					)");
				
				m_database.Update(@"
					CREATE TABLE IF NOT EXISTS Fields(
						name TEXT NOT NULL 
							CONSTRAINT no_empty_name CHECK(length(name) > 0),
						declaring_root_name TEXT NOT NULL 
							REFERENCES Types(root_name),
						type_name TEXT NOT NULL 
							CONSTRAINT no_empty_type CHECK(length(type_name) > 0),
						assembly INTEGER NOT NULL 
							REFERENCES Assemblies(assembly),
						access INTEGER NOT NULL
							CONSTRAINT valid_access CHECK(access >= 0 AND access <= 3),
						static INTEGER NOT NULL
							CONSTRAINT valid_static CHECK(static >= 0 AND static <= 1),
						PRIMARY KEY(name, declaring_root_name)
					)");
			});
		}
		
		private void DoDeleteAssemblies()		// threaded
		{
			m_database.Update("prune assemblies", () =>
			{
				// Remove every row in Assemblies where the path is no longer valid.
				string sql = @"
					SELECT path, assembly, name, culture, version
						FROM Assemblies";
				string[][] rows = m_database.QueryRows(sql);
				
				foreach (string[] row in rows)
				{
					if (!File.Exists(row[0]))
					{
						Log.WriteLine(TraceLevel.Info, "ObjectModel", "pruning {0} {1} {2}", row[2], row[3], row[4]);
						
						m_database.Update(string.Format(@"
							DELETE FROM Assemblies 
								WHERE path = '{0}'", row[0]));
								
						DoDeleteAssemblyReferences(row[1]);
					}
				}
			});
		}
		
		private void DoDeleteAssemblyReferences(string id)		// threaded
		{
			m_database.Update(string.Format(@"
				DELETE FROM Types 
					WHERE assembly = '{0}'", id));
			
			m_database.Update(string.Format(@"
				DELETE FROM Methods 
					WHERE assembly = '{0}'", id));
			
			m_database.Update(string.Format(@"
				DELETE FROM Fields 
					WHERE assembly = '{0}'", id));
		}
		
		private void DoUpdateAssemblies(string path)		// threaded
		{
			try
			{
				AssemblyDefinition assembly = null;
				string id = "0";
				DoTryAddAssembly(path, ref assembly, ref id);
				
				// If we need to process the assembly then,
				if (assembly != null)
				{
					// parse the assembly (we do this last so that the Types table
					// can refer to the new row in the Assemblies table),
					bool fullParse = !path.Contains("/gac/") && !path.Contains("/mscorlib.dll") && File.Exists(path + ".mdb");	// TODO: might want to optionally allow full parse of mscorlib and assemblies in the gac			
					DoParseAssembly(path, assembly, id, fullParse);
					
					// and queue up any assemblies it references (but only for local assemblies:
					// the database are already large and transitively parsing all assemblies isn't
					// that useful).
					if (fullParse)
						DoQueueReferencedAssemblies(assembly, path);
				}
			}
			catch (BadImageFormatException)
			{
				// not an assembly
			}
			catch (ImageFormatException)
			{
				// not an assembly
			}
			catch (IOException ie)
			{
				// the file system may change as we are trying to process assemblies
				// so we'll ignore IO errors (in release anyway)
				Log.WriteLine(TraceLevel.Info, "Errors", "error analyzing '{0}':", path);
				Log.WriteLine(TraceLevel.Info, "Errors", ie.Message);
			}
			catch (Exception e)
			{
				Console.Error.WriteLine("error analyzing '{0}':", path);
				Console.Error.WriteLine(e);
			}
		}
		
		private void DoQueueReferencedAssemblies(AssemblyDefinition assembly, string path)		// threaded
		{
			var resolver = (BaseAssemblyResolver) assembly.Resolver;
			resolver.AddSearchDirectory(Path.GetDirectoryName(path));
			
			foreach (ModuleDefinition module in assembly.Modules)
			{
				foreach (AssemblyNameReference nr in module.AssemblyReferences)
				{
					try
					{
						if (!m_resolvedAssemblies.Contains(nr.FullName))
						{
							AssemblyDefinition ad = resolver.Resolve(nr);	// this is a little inefficient because we load the assembly twice, but the load is not the bottleneck...
							m_resolvedAssemblies.Add(nr.FullName);
							
							Image image = ad.MainModule.Image;
							Log.WriteLine(TraceLevel.Verbose, "ObjectModel", "resolved {0} at {1}", nr.FullName, image.FileInformation.FullName);
							
							lock (m_lock)
							{
//		Console.WriteLine("adding referenced file {0} for thread {1}", image.FileInformation.FullName, Thread.CurrentThread.ManagedThreadId);
								m_files.Add(image.FileInformation.FullName);	// note that we don't need to pulse because we execute within the thread
							}
						}
					}
					catch
					{
						Log.WriteLine(TraceLevel.Verbose, "ObjectModel", "Couldn't resolve {0}", nr.FullName);	// this is fairly common with intermediate build steps when packaging bundles
					}
				}
			}
		}
		
		private void DoTryAddAssembly(string path, ref AssemblyDefinition assembly, ref string id)		// threaded
		{
			AssemblyDefinition candidate = AssemblyCache.Load(path, true);
			string candidateID = null;
			
			m_database.Update("update assemblies for " + path, () =>
			{
				string culture = string.IsNullOrEmpty(candidate.Name.Culture) ? "neutral" : candidate.Name.Culture.ToLower();
				string sql = string.Format(@"
					SELECT version, write_time, assembly
						FROM Assemblies
					WHERE name = '{0}' AND culture = '{1}'", candidate.Name.Name, culture);
				string[][] rows = m_database.QueryRows(sql);
		
				Version currentVersion = candidate.Name.Version;
				long currentTicks = File.GetLastWriteTime(path).Ticks;
				if (DoCurrentIsNewer(currentVersion, currentTicks, rows))
				{
					string oldID = DoFindNewest(rows);
					if (oldID != null)
						DoDeleteAssemblyReferences(oldID);
					
					sql = @"SELECT COALESCE(MAX(assembly), 1) FROM Assemblies";
					rows = m_database.QueryRows(sql);
					candidateID = (rows.Length > 0 ? int.Parse(rows[0][0]) + 1 : 1).ToString();
					
					m_database.InsertOrReplace("Assemblies",
						path,
						candidate.Name.Name,
						culture,
						currentVersion.ToString(),
						currentTicks.ToString(),
						candidateID);
				}
				else
				{
					Log.WriteLine(TraceLevel.Info, "ObjectModel", "Ignoring {0}", candidate);
					candidate = null;
				}
			});
			
			assembly = candidate;
			id = candidateID;
		}
		
		private string DoFindNewest(string[][] rows)
		{
			string id = null;
			
			if (rows.Length > 0)
			{
				Version currentVersion = new Version(rows[0][0]);
				long currentTicks = long.Parse(rows[0][1]);
				id = rows[0][2];
				
				for (int i = 1; i < rows.Length; ++i)
				{
					if (!DoCurrentIsNewer(currentVersion, currentTicks, rows[i][0], rows[i][1]))
					{
						currentVersion = new Version(rows[i][0]);
						currentTicks = long.Parse(rows[i][1]);
						id = rows[i][2];
					}
				}
			}
			
			return id;
		}
		
		private bool DoCurrentIsNewer(Version currentVersion, long currentTicks, string[][] rows)
		{
			bool newer = true;
			
			for (int i = 0; i < rows.Length && newer; ++i)
			{
				newer = DoCurrentIsNewer(currentVersion, currentTicks, rows[i][0], rows[i][1]);
			}
			
			return newer;
		}
		
		private bool DoCurrentIsNewer(Version currentVersion, long currentTicks, string oldVersion, string oldTicks)
		{
			bool newer = true;
			
			Version version = new Version(oldVersion);
			if (version > currentVersion)
			{
				newer = false;
			}
			else if (version == currentVersion)
			{
				if (long.Parse(oldTicks) >= currentTicks)
					newer = false;
			}
			
			return newer;
		}
		
		private void DoParseAssembly(string path, AssemblyDefinition assembly, string id, bool fullParse)		// threaded
		{
			m_boss.CallRepeated<IParseAssembly>(i => i.Parse(path, assembly, id, fullParse));
		}
		#endregion
		
		#region Fields
		private Thread m_thread;
		private string m_path;
		private Boss m_boss;
		private Database m_database;
		private List<string> m_resolvedAssemblies = new List<string>();
		private List<DirectoryWatcher> m_watchers = new List<DirectoryWatcher>();
		private object m_lock = new object();
			private bool m_running = true;
			private List<string> m_files = new List<string>();
		#endregion
	}
}
