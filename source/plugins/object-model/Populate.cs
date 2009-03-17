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
using System.Security.Cryptography;
using System.Threading;

namespace ObjectModel
{
	internal sealed class Populate : IOpened, IShutdown
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
			
			string path = Path.Combine(Paths.SupportPath, name + "2.db");
	
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
			string path = Populate.GetDatabasePath(m_boss);
			Log.WriteLine("ObjectModel", "'{0}' was opened", path);
			Log.WriteLine("Database", "creating populate database");
			m_database = new Database(path);
			
			Broadcaster.Register("opened directory", this, this.DoOnOpenDir);
			
			// Setting this to NORMAL or OFF does not make much of a difference
			// in the parse times because we're using transactions...
//			m_database.Update("PRAGMA synchronous = NORMAL");
			
			// We could place an upper limit on some of the field sizes, but it won't
			// do much good because sqlite stores just what is needed.
			Log.WriteLine(TraceLevel.Verbose, "ObjectModel", "creating tables");
			m_database.Update("create tables", () =>
			{
				m_database.Update(@"
					CREATE TABLE IF NOT EXISTS Assemblies(
						hash TEXT NOT NULL PRIMARY KEY
							CONSTRAINT hash_size CHECK(length(hash) >= 8),
						name TEXT NOT NULL
							CONSTRAINT no_empty_name CHECK(length(name) > 0),
						culture TEXT NOT NULL
							CONSTRAINT no_empty_culture CHECK(length(culture) > 0),
						major INTEGER NOT NULL
							CONSTRAINT sane_major CHECK(major >= 0),
						minor INTEGER NOT NULL
							CONSTRAINT sane_minor CHECK(minor >= 0),
						build INTEGER NOT NULL
							CONSTRAINT sane_build CHECK(build >= 0),
						revision INTEGER NOT NULL
							CONSTRAINT sane_revision CHECK(revision >= 0)
					)");
				
				// TODO: we should probably be using an assembly_id instead of a hash
				// for the foreign keys. That should be a bit faster and more space efficient.
				m_database.Update(@"
					CREATE TABLE IF NOT EXISTS AssemblyPaths(
						path TEXT NOT NULL PRIMARY KEY
							CONSTRAINT absolute_path CHECK(substr(path, 1, 1) = '/'),
						hash TEXT NOT NULL REFERENCES Assemblies(hash),
						write_time INTEGER NOT NULL
							CONSTRAINT sane_time CHECK(write_time > 0)
					)");
			});
			
			Log.WriteLine(TraceLevel.Verbose, "ObjectModel", "starting thread");
			m_thread = new Thread(this.DoParseAssemblies);
			m_thread.Name = "parse assemblies";
			m_thread.IsBackground = false;
			m_thread.Priority = ThreadPriority.BelowNormal;		// this is ignored on Mono 2.0
			m_thread.Start();
//	Console.WriteLine("{0} is using thread {1}", Path.GetFileName(path), m_thread.ManagedThreadId);
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
		
		private void DoParseAssemblies()	// threaded
		{
			while (true)
			{
				try
				{
					DoPruneAssemblies();
					Log.WriteLine(TraceLevel.Verbose, "ObjectModel", "pruned assemblies");
					
					string root;
					lock (m_lock)
					{
						while (m_files.Count == 0 && m_running)
						{
//	Console.WriteLine("thread {0} is blocking", Thread.CurrentThread.ManagedThreadId);
							Log.WriteLine(TraceLevel.Verbose, "ObjectModel", "waiting");
							Unused.Value = Monitor.Wait(m_lock);
						}
						
						if (!m_running)
							break;
						
						root = m_files[0];
						m_files.RemoveAt(0);
					}
					
					if (!root.Contains(".app/") && !root.Contains("/.svn"))
					{
						DoUpdateAssemblyPaths(root);
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
		}
				
		private void DoPruneAssemblies()		// threaded
		{
			m_database.Update("prune assemblies", () =>
			{
				// Remove every row in AssemblyPaths where the path is no longer valid.
				string sql = @"
					SELECT path, hash 
						FROM AssemblyPaths";
				string[][] rows = m_database.QueryRows(sql);
				
				foreach (string[] row in rows)
				{
					if (!File.Exists(row[0]))
					{
						Log.WriteLine(TraceLevel.Verbose, "ObjectModel", "removing '{0}' from AssemblyPaths", row[0]);
						
						m_database.Update(string.Format(@"
							DELETE FROM AssemblyPaths 
								WHERE path = '{0}' AND hash = '{1}'", row[0], row[1]));
					}
				}
				
				// Remove every row in Assemblies which no longer has a path.
				sql = @"
					SELECT DISTINCT hash 
						FROM Assemblies
					EXCEPT SELECT hash
						FROM AssemblyPaths";
				rows = m_database.QueryRows(sql);
				
				foreach (string[] row in rows)
				{
					if (Log.IsEnabled(TraceLevel.Info, "ObjectModel"))
					{
						sql = string.Format(@"
							SELECT DISTINCT name, major, minor, build, revision 
								FROM Assemblies
							WHERE hash = '{0}'", row[0]);
						string[][] temp = m_database.QueryRows(sql);
						
						if (temp.Length > 0)
						{
							string name = string.Format("{0} {1}.{2}.{3}.{4}", temp[0][0], temp[0][1], temp[0][2], temp[0][3], temp[0][4]);	
							Log.WriteLine(TraceLevel.Verbose, "ObjectModel", "removing '{0}' from Assemblies (no assemblies with its hash exist)", name);
						}
					}
					
					DoCascadeHash("Types", row[0]);
					DoCascadeHash("Implements", row[0]);
					DoCascadeHash("Methods", row[0]);
					DoCascadeHash("NameInfo", row[0]);
					DoCascadeHash("ExtensionMethods", row[0]);
					DoCascadeHash("Fields", row[0]);
					
					m_database.Update(string.Format(@"
						DELETE FROM Assemblies 
							WHERE hash = '{0}'", row[0]));
				}
			});
		}
		
		// sqlite does not support foreign key constraints so we need to do it ourselves.
		private void DoCascadeHash(string table, string hash)
		{
			m_database.Update(string.Format(@"
				DELETE FROM {0} 
					WHERE hash = '{1}'", table, hash));
		}
		
		private void DoUpdateAssemblyPaths(string path)		// threaded
		{
			try
			{
				Log.WriteLine(TraceLevel.Verbose, "ObjectModel", "checking parse date for '{0}'", path);
				
				// Find out when (or if we ever) processed the file.
				string sql = string.Format(@"
					SELECT write_time 
						FROM AssemblyPaths 
					WHERE path='{0}'", path);
				string[][] rows = m_database.QueryRows(sql);
				Trace.Assert(rows.Length <= 1, string.Format("got {0} rows looking for {1}", rows.Length, path));
				
				// If we've never processed the file or it has changed since we
				// last processed it then,
				bool dirty = rows.Length == 0;
				long currentTicks = File.GetLastWriteTime(path).Ticks;
				if (!dirty)
				{
					long cachedTicks = long.Parse(rows[0][0]);
					dirty = currentTicks > cachedTicks;
				}
				
				if (dirty)
				{
					// update the database using the contents of the assembly,
					byte[] contents = File.ReadAllBytes(path);
					byte[] hash = m_hasher.ComputeHash(contents);
					DoUpdateAssemblies(path, hash);
					
					// and update the database with the new time (we have to do
					// this after DoUpdateAssemblies so that the new row has a
					// valid foreign key).
					m_database.Update("update assembly path " + path, () =>
					{
						m_database.InsertOrReplace("AssemblyPaths",
							path, BitConverter.ToString(hash), currentTicks.ToString());
					});
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
		
		// It shouldn't normally be necessary to do the query and insert within a transaction
		// but it may be needed if both Continuum and Foreshadow are editing the same
		// directory.
		private AssemblyDefinition DoUpdateAssemby(string path, byte[] hash)		// threaded
		{
			AssemblyDefinition assembly = null;
			
			string tname = "update assemblies for " + path;
			try
			{
				m_database.Begin(tname);
				
				// See if the assembly is one we have already processed.
				string sql = string.Format(@"
					SELECT name 
						FROM Assemblies 
					WHERE hash='{0}'", BitConverter.ToString(hash));
				string[][] rows = m_database.QueryRows(sql);
				Trace.Assert(rows.Length <= 1, string.Format("got {0} rows looking for hash {1}", rows.Length, BitConverter.ToString(hash)));
				
				// If not then,
				if (rows.Length == 0)
				{
					// load the assembly,
					assembly = AssemblyCache.Load(path, true);	// TODO: there doesn't appear to be a way to use the contents array and still be able to load symbols
					
					// update the Assemblies table.
					m_database.Insert("Assemblies",
						BitConverter.ToString(hash),
						assembly.Name.Name,
						string.IsNullOrEmpty(assembly.Name.Culture) ? "neutral" : assembly.Name.Culture.ToLower(),
						assembly.Name.Version.Major.ToString(),
						assembly.Name.Version.Minor.ToString(),
						assembly.Name.Version.Build.ToString(),
						assembly.Name.Version.Revision.ToString());
				}
				
				m_database.Commit(tname);
			}
			catch
			{
				m_database.Rollback(tname);
				throw;
			}
			
			return assembly;
		}
		
		private void DoUpdateAssemblies(string path, byte[] hash)		// threaded
		{
			AssemblyDefinition assembly = DoUpdateAssemby(path, hash);
			
			// If we need to process the assembly then,
			if (assembly != null)
			{
				// parse the assembly (we do this last so that the Types table
				// can refer to the new row in the Assemblies table),
				bool fullParse = !path.Contains("/gac/") && !path.Contains("/mscorlib.dll") && File.Exists(path + ".mdb");	// TODO: might want to optionally allow full parse of mscorlib and assemblies in the gac			
				DoParseAssembly(path, assembly, BitConverter.ToString(hash), fullParse);
				
				// and queue up any assemblies it references (but only for local assemblies:
				// the database are already large and transitively parsing all assemblies isn't
				// that useful).
				if (fullParse)
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
			}
		}
		
		private void DoParseAssembly(string path, AssemblyDefinition assembly, string hash, bool fullParse)		// threaded
		{
			int order = DoCompareAssembly(assembly.Name, hash);
			
			// Only parse assemblies that have the same or a newer version numbers as
			// assemblies we have already parsed. TODO: we do this to avoid matching
			// old types, but it would be better to be smarter about the assemblies we
			// try to match.
			if (order >= 0)
			{
				m_boss.CallRepeated<IParseAssembly>(i => i.Parse(path, assembly, hash, fullParse));
				
				// If the assembly is newer then remove the old assemblies types and methods.
				if (order == 1)
					DoPruneOldVersions(assembly.Name, hash);
			}
			else
				Log.WriteLine(TraceLevel.Verbose, "ObjectModel", "skipping {0} {1} (it's an older version)", assembly.Name.Name, assembly.Name.Version);
		}
		
		// Return +1 if the specified assembly has a higher version than all the other assemblies,
		// 0 if it is as high as the other highest assembly, and -1 otherwise.
		private int DoCompareAssembly(AssemblyNameReference name, string hash)	// theaded
		{
			string sql = string.Format(@"
				SELECT DISTINCT major, minor, build, revision
					FROM Assemblies
				WHERE name = '{0}' AND culture = '{1}' AND hash != '{2}'", name.Name, string.IsNullOrEmpty(name.Culture) ? "neutral" : name.Culture.ToLower(), hash);
			string[][] rows = m_database.QueryRows(sql);
			
			int result = -1;
			if (rows.All(r =>					// note that All returns true if the sequence is empty
			{
				Version old = new Version(int.Parse(r[0]), int.Parse(r[1]), int.Parse(r[2]), int.Parse(r[3]));
				return name.Version > old;
			}))
			{
				result = +1;
			}
			else if (rows.All(r =>
			{
				Version old = new Version(int.Parse(r[0]), int.Parse(r[1]), int.Parse(r[2]), int.Parse(r[3]));
				return name.Version >= old;
			}))
			{
				result = 0;
			}
			
			return result;
		}
		
		private void DoPruneOldVersions(AssemblyNameReference name, string hash)	// theaded
		{
			m_database.Update("prune old versions of " + name.FullName, () =>
			{
				string sql = string.Format(@"
					SELECT DISTINCT major, minor, build, revision, hash
						FROM Assemblies
					WHERE name = '{0}' AND culture = '{1}' AND hash != '{2}'", name.Name, string.IsNullOrEmpty(name.Culture) ? "neutral" : name.Culture.ToLower(), hash);
				string[][] rows = m_database.QueryRows(sql);
				
				foreach (string[] r in rows)
				{
					Version rhs = new Version(int.Parse(r[0]), int.Parse(r[1]), int.Parse(r[2]), int.Parse(r[3]));
					if (name.Version > rhs)
					{
						Log.WriteLine(TraceLevel.Verbose, "ObjectModel", "pruning {0} {1}.{2}.{3}.{4} (there's a newer version)", name.Name, r[0], r[1], r[2], r[3]);
						
						m_database.Update(string.Format(@"
							DELETE FROM Types 
								WHERE hash = '{0}'", r[4]));
						
						m_database.Update(string.Format(@"
							DELETE FROM Implements 
								WHERE hash = '{0}'", r[4]));
						
						m_database.Update(string.Format(@"
							DELETE FROM Methods 
								WHERE hash = '{0}'", r[4]));
						
						m_database.Update(string.Format(@"
							DELETE FROM ExtensionMethods 
								WHERE hash = '{0}'", r[4]));
						
						m_database.Update(string.Format(@"
							DELETE FROM Fields 
								WHERE hash = '{0}'", r[4]));
					}
				}
			});
		}
		#endregion
		
		#region Fields
		private Thread m_thread;
		private Boss m_boss;
		private MD5 m_hasher = MD5.Create();				// md5 isn't cryptographically secure any more, but that's OK: we just want a good hash to minimize duplicate work
		private Database m_database;
		private List<string> m_resolvedAssemblies = new List<string>();
		private List<DirectoryWatcher> m_watchers = new List<DirectoryWatcher>();
		private object m_lock = new object();
			private bool m_running = true;
			private List<string> m_files = new List<string>();
		#endregion
	}
}
