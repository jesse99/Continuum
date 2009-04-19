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
					Contract.Assert(false, "bad name: " + name);
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
					
					var files = new List<string>();
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
							
						files.AddRange(m_files);
						m_files.Clear();
					}
					
					bool added = false;
					foreach (string file in files)
					{
						if (!file.Contains(".app/") && !file.Contains("/.svn"))
						{
							if (DoAddAssembly(file))
								added = true;
						}
					}
					
					if (added)
						DoCheckAssemblyVersions();
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
							CONSTRAINT no_zero_assembly CHECK(assembly != 0),
						in_use INTEGER NOT NULL
							CONSTRAINT valid_in_use CHECK(in_use >= 0 AND in_use <= 1)
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
						base_root_name TEXT NOT NULL,
						interface_root_names TEXT NOT NULL
							CONSTRAINT valid_interfaces CHECK(length(interface_root_names) = 0 OR substr(interface_root_names, -1) = ':'),
						generic_arg_count INTEGER NOT NULL
							CONSTRAINT non_negative_arg_count CHECK(generic_arg_count >= 0),
						generic_arg_names TEXT NOT NULL,
						visibility INTEGER NOT NULL
							CONSTRAINT valid_vis CHECK(visibility >= 0 AND visibility <= 3),
						attributes INTEGER NOT NULL
							CONSTRAINT valid_attributes CHECK(attributes >= 0 AND attributes <= 127)
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
							CONSTRAINT valid_kind CHECK(kind >= 0 AND kind <= 3),
						kind_name TEXT NOT NULL
							CONSTRAINT no_empty_kind_name CHECK(length(kind_name) > 0)
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
							CONSTRAINT valid_kind CHECK(kind >= 0 AND kind <= 9)
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
			int count = 0;
			
			m_database.Update("prune assemblies", () =>
			{
				// Remove every row in Assemblies where the path is no longer valid.
				string sql = @"
					SELECT version, assembly, path, name, culture, in_use
						FROM Assemblies";
				NamedRows rows = m_database.QueryNamedRows(sql);
				
				foreach (NamedRow row in rows)
				{
					if (!File.Exists(row["path"]))
					{
						Log.WriteLine(TraceLevel.Info, "ObjectModel", "removing deleted {0} {1} {2}", row["name"], row["culture"], row["version"]);
						
						m_database.Update(string.Format(@"
							DELETE FROM Assemblies 
								WHERE path = '{0}'", row["path"]));
						DoDeleteAssemblyReferences(row["assembly"]);
						
						if (row["in_use"] == "1")
							++count;
					}
				}
			});
			
			// If we deleted an assembly which was in use then we need to check to
			// see if there's an older version of that assembly we can use.
			if (count > 0)
			{
				Log.WriteLine(TraceLevel.Verbose, "ObjectModel", "{0} in-use assemblies were deleted", count);
				DoCheckAssemblyVersions();
			}
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
		
		private bool DoAddAssembly(string path)
		{
			Contract.Requires(Path.IsPathRooted(path), path + " is not an absolute path");
			
			bool added = false;
			
			try
			{
				m_database.Update("add assembly " + path, () =>
				{
					string ticks = File.GetLastWriteTime(path).Ticks.ToString();
					
					string sql = string.Format(@"
						SELECT version, write_time, assembly, in_use
							FROM Assemblies
						WHERE path = '{0}'", path.Replace("'", "''"));
					NamedRows rows = m_database.QueryNamedRows(sql);
					Contract.Assert(rows.Length <= 1, "too many rows");
					
					// We don't check the version because if the assemblies version is newer then
					// we want to use it, if it is equal then that doesn't tell us anything (because
					// version numbers may not change when assemblies are built), and because
					// if the version is somehow older then we want to record that fact. And, of
					// course, we'd like to avoid loading the assembly if we don't need to use it.
					if (rows.Length == 0 || rows[0]["write_time"] != ticks)
					{
						// Note that we want to do this before we delete the old references because
						// the load may fail (e.g. if a build is in progress and files/directories are
						// being deleted).
						AssemblyDefinition assembly = AssemblyCache.Load(path, true);
						string culture = string.IsNullOrEmpty(assembly.Name.Culture) ? "neutral" : assembly.Name.Culture.ToLower();
						Version version = assembly.Name.Version;
						
						if (rows.Length > 0 && rows[0]["in_use"] == "1")
							DoDeleteAssemblyReferences(rows[0]["assembly"]);
						
						sql = @"SELECT COALESCE(MAX(assembly), 0) FROM Assemblies";
						string[][] rs = m_database.QueryRows(sql);
						int id = int.Parse(rs[0][0]) + 1;
						
						Log.WriteLine(TraceLevel.Verbose, "ObjectModel", "adding new {0} {1} {2}", assembly.Name.Name, culture, version);
						m_database.InsertOrReplace("Assemblies",
							path,
							assembly.Name.Name,
							culture,
							version.ToString(),
							ticks,
							id.ToString(),
							"0");
							
							added = true;
					}
					else
						Log.WriteLine(TraceLevel.Verbose, "ObjectModel", "ignoring {0}", Path.GetFileName(path));
				});
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
				// so we'll ignore IO errors
				Log.WriteLine(TraceLevel.Info, "Errors", "error adding '{0}':", path);
				Log.WriteLine(TraceLevel.Info, "Errors", ie.Message);
			}
			catch (Exception e)
			{
				Console.Error.WriteLine("error adding '{0}':", path);
				Console.Error.WriteLine(e);
			}
			
			return added;
		}
		
		// Make sure the assembly which is in use is the newest version.
		private void DoCheckAssemblyVersions()
		{
			string sql = @"
				SELECT DISTINCT name, culture
					FROM Assemblies";
			NamedRows rows = m_database.QueryNamedRows(sql);
			foreach (NamedRow row in rows)
			{
				NamedRow newest = DoGetNewestAssembly(row["name"], row["culture"]);
				NamedRow current = DoGetUsedAssembly(row["name"], row["culture"]);
				if (current.Arity == 0 || newest["path"] != current["path"])
				{
					if (current.Arity > 0)
						DoUnparseAssembly(current);
					
					DoParseAssembly(newest);
				}
			}
		}
		
		private NamedRow DoGetNewestAssembly(string name, string culture)
		{
			string sql = string.Format(@"
				SELECT path, name, culture, version, write_time, assembly, in_use
					FROM Assemblies
				WHERE name = '{0}' AND culture = '{1}'", name, culture);
			NamedRows rows = m_database.QueryNamedRows(sql);
			
			NamedRow newest = new NamedRow();
			foreach (NamedRow row in rows)
			{
				if (newest.Arity == 0 || DoLhsIsNewer(row, newest))
					newest = row;
			}
			Contract.Assert(newest.Arity > 0, "should have found at least one assembly named " + name);
			
			return newest;
		}
		
		private NamedRow DoGetUsedAssembly(string name, string culture)
		{
			string sql = string.Format(@"
				SELECT path, name, culture, version, write_time, assembly, in_use
					FROM Assemblies
				WHERE name = '{0}' AND culture = '{1}'", name, culture);
			NamedRows rows = m_database.QueryNamedRows(sql);
			
			NamedRow used = new NamedRow();
			foreach (NamedRow row in rows)
			{
				if (row["in_use"] == "1")
				{
					Contract.Assert(used.Arity == 0, string.Format("two assemblies named {0} {1} are both in use", name, culture));
					used = row;						// there shouldn't be many assemblies named name so continuing won't have much of an impact on performance (and allows us to check part of the Assemblies invariant)
				}
			}
			
			return used;
		}
		
		private bool DoLhsIsNewer(NamedRow lhs, NamedRow rhs)
		{
			bool newer = false;
			
			Version lhsVersion = new Version(lhs["version"]);
			Version rhsVersion = new Version(rhs["version"]);
			
			if (lhsVersion > rhsVersion)
			{
				newer = true;
			}
			else if (lhsVersion == rhsVersion)
			{
				long lhsTicks = long.Parse(lhs["write_time"]);
				long rhsTicks = long.Parse(rhs["write_time"]);
				
				if (lhsTicks > rhsTicks)
					newer = true;
			}
			
			return newer;
		}
		
		private void DoUnparseAssembly(NamedRow row)
		{
			Contract.Requires(row["in_use"] == "1", "assembly is not in use");
			
			m_database.Update("unparsed", () =>
			{
				Log.WriteLine(TraceLevel.Verbose, "ObjectModel", "unparsing {0} {1} {2}", row["name"], row["culture"], row["version"]);
				DoDeleteAssemblyReferences(row["assembly"]);
				
				m_database.InsertOrReplace("Assemblies",
					row["path"],
					row["name"],
					row["culture"],
					row["version"],
					row["write_time"],
					row["assembly"],
					"0");
			});
		}
		
		private void DoParseAssembly(NamedRow row)
		{
			Contract.Requires(row["in_use"] == "0", "assembly is already in use");
			
			string path = row["path"];
			
			try
			{
				bool fullParse = !path.Contains("/gac/") && !path.Contains("/mscorlib.dll") && File.Exists(path + ".mdb");	// TODO: might want to optionally allow full parse of mscorlib and assemblies in the gac
				AssemblyDefinition assembly = AssemblyCache.Load(path, true);
				
				Log.WriteLine(TraceLevel.Info, "ObjectModel", "parsing {0} {1} {2}", row["name"], row["culture"], row["version"]);
				m_boss.CallRepeated<IParseAssembly>(i => i.Parse(path, assembly, row["assembly"], fullParse));
				
				m_database.Update("parsed", () =>
				{
					m_database.InsertOrReplace("Assemblies",
						path,
						row["name"],
						row["culture"],
						row["version"],
						row["write_time"],
						row["assembly"],
						"1");
				});
				
				if (fullParse)
					DoQueueReferencedAssemblies(assembly, path);
			}
			catch (IOException ie)
			{
				// the file system may change as we are trying to process assemblies
				// so we'll ignore IO errors
				Log.WriteLine(TraceLevel.Info, "Errors", "error parsing '{0}':", path);
				Log.WriteLine(TraceLevel.Info, "Errors", ie.Message);
			}
			catch (Exception e)
			{
				Console.Error.WriteLine("error parsing '{0}':", path);
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
							Log.WriteLine(TraceLevel.Verbose, "ObjectModel", "queuing referenced {0} {1} {2}", nr.Name, nr.Culture, nr.Version);
							
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
