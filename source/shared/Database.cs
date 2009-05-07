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

using Gear.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Threading;

namespace Shared
{
	[Serializable]
	public class DatabaseException : Exception
	{
		public DatabaseException()
		{
		}
		
		public DatabaseException(string text) : base(text)
		{
		}
		
		public DatabaseException(string text, Exception inner) : base (text, inner)
		{
		}
		
		[SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
		protected DatabaseException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}
	
	[Serializable]
	public sealed class DatabaseLockedException : DatabaseException
	{
		public DatabaseLockedException()
		{
		}
		
		public DatabaseLockedException(string text) : base(text) 
		{
		}
		
		public DatabaseLockedException(string text, Exception inner) : base (text, inner)
		{
		}
		
		[SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
		private DatabaseLockedException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}
	
	[ThreadModel(ThreadModel.Concurrent)]
	public struct NamedRow
	{
		internal NamedRow(Dictionary<string, int> names, string[] row)
		{
			m_names = names;
			m_row = row;
		}
		
		public int Arity
		{
			get {return m_names != null ? m_names.Count : 0;}
		}
		
		public string this[string name]
		{
			get
			{
				int index = m_names[name];
				return m_row[index];
			}
		}
		
		public string this[int index]
		{
			get
			{
				return m_row[index];
			}
		}
		
		public override string ToString()
		{
			var builder = new System.Text.StringBuilder();
			builder.Append("{");
			
			int i = 0;
			foreach (var entry in m_names)
			{
				builder.Append(entry.Key);
				builder.Append(" = ");
				builder.Append(m_row[entry.Value]);
				
				if (++i < m_names.Count)
					builder.Append(", ");
			}
			
			builder.Append("}");
			
			return builder.ToString();
		}
		
		private readonly Dictionary<string, int> m_names;
		private readonly string[] m_row;
	}
	
	[ThreadModel(ThreadModel.Concurrent)]
	public sealed class NamedRows : IEnumerable<NamedRow>
	{
		public NamedRows(Dictionary<string, int> names, string[][] rows)
		{
			Contract.Requires(names != null, "names is null");
			Contract.Requires(rows != null, "rows is null");
			Contract.Requires(rows.Length == 0 || names.Count == rows[0].Length, "names and row lengths don't match");
			
			m_names = names;
			m_rows = rows;
		}
		
		public int Length
		{
			get {return m_rows.Length;}
		}
		
		public int Arity
		{
			get {return m_names.Count;}
		}
		
		public NamedRow this[int index]
		{
			get
			{
				string[] row = m_rows[index];
				return new NamedRow(m_names, row);
			}
		}
		
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
		
		public IEnumerator<NamedRow> GetEnumerator()
		{
			for (int i = 0; i < m_rows.Length; ++i)
				yield return new NamedRow(m_names, m_rows[i]);
		}
		
		private readonly Dictionary<string, int> m_names;
		private readonly string[][] m_rows;
	}
	
	// Simple sqlite wrapper.
	[ThreadModel(ThreadModel.ArbitraryThread)]
	public sealed class Database	: IDisposable
	{
		[ThreadModel("finalizer")]
		~Database()
		{
			DoDispose(false);
		}
		
		// Opens a connection to an arbitrary database. Name is arbitrary and used for
		// debugging locking problems.
		public Database(string path, string name)
		{
			Contract.Requires(!string.IsNullOrEmpty(path), "path is null or empty");
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			Log.WriteLine(TraceLevel.Verbose, "Database", "connecting to database at {0}", path);
			
			if (sqlite3_threadsafe() == 0)
				throw new InvalidOperationException("libsqlite3.dylib was built without thread support.");
			
			m_name = name;
			m_threadID = Thread.CurrentThread.ManagedThreadId;
			DoSaveThis(this);
			
			OpenFlags flags = OpenFlags.READWRITE | OpenFlags.CREATE | OpenFlags.FULLMUTEX;
			Error err = sqlite3_open_v2(path, out m_database, flags, IntPtr.Zero);
			if (err != Error.OK)
			{
				string mesg = string.Format("Failed to open '{0}' ({1})", path, err);
				throw new DatabaseException(mesg);
			}
			else if (m_database == IntPtr.Zero)
			{
				string mesg = string.Format("Failed to open '{0}')", path);
				throw new DatabaseException(mesg);
			}
			
			Unused.Value = sqlite3_busy_timeout(m_database, 5*1000);
		}
		
		// Used for SQL commands which do not return a table.
		public void Update(string command)
		{
			Contract.Requires(!string.IsNullOrEmpty(command), "command is null or empty");
			Contract.Requires(m_lock != null, "update is not within a transaction");
			Contract.Requires(Thread.CurrentThread.ManagedThreadId == m_threadID, m_name + " was used with multiple threads");
			if (m_disposed)
				throw new ObjectDisposedException(GetType().Name);
			
			Log.WriteLine(TraceLevel.Verbose, "Database", "update: {0}", command);
			
			try
			{
				m_command = command;
				
				IntPtr errMesg = IntPtr.Zero;
				Error err = sqlite3_exec(m_database, command, null, IntPtr.Zero, out errMesg);
				
				if (err != Error.OK)
					DoRaiseError(command, err, errMesg);
			}
			finally
			{
				m_command = null;
			}
			
			GC.KeepAlive(this);
		}
		
		// We use this instead of System.Action so that it gets the right threading attribute.
		public delegate void UpdateCallback();
		
		// When a connection calls BEGIN IMMEDIATE it will transition from UNLOCKED
		// (can't read/write) to SHARED (can read) to RESERVED (the connection plans
		// on writing). If BEGIN IMMEDIATE returns successfully other connections may
		// be in SHARED but none will be in RESERVED (or higher).
		//
		// When a connection calls COMMIT it will transition to PENDING (it wants to
		// write) to EXCLUSIVE (it will write). If a connection has the PENDING lock then
		// no other connections can acquire a new lock.
		//
		// If a connection cannot acquire a lock it will normally do a busy wait using the
		// handler we installed with sqlite3_busy_timeout. However if a thread is in the
		// middle of BEGIN IMMEDIATE and another thread is in the middle of COMMIT
		// it's possible that they will be deadlocked (see <http://www.sqlite.org/c3ref/busy_handler.html>).
		// If this happens the first thread will return SQLITE_BUSY.
		//
		// In order to simplify life for our callers we'll address this by sleeping and 
		// restarting the first transaction a few times. Because we're restarting the
		// entire transaction we avoid the deadlock.
		public void Update(string name, UpdateCallback action)
		{
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			Contract.Requires(action != null, "action is null");
			if (m_disposed)
				throw new ObjectDisposedException(GetType().Name);
			
			int i = 0;
			
			while (true)
			{
				try
				{
					UnsafeBegin(name);
					action();
					UnsafeCommit(name);
					break;
				}
				catch (DatabaseLockedException)
				{
					const int MaxTries = 5;
					
					UnsafeRollback(name);
					if (++i < MaxTries)
					{
						Log.WriteLine(TraceLevel.Info, "Database", "Transactions deadlocked:");
						m_command = string.Format("failed to acquire a lock for {0}, try {1} of {2}", name, i, MaxTries);
						DoDumpState(TraceLevel.Info);
						
						Thread.Sleep(100);		// max commit time is 400 msecs on a fast machine
					}
					else
					{
						Log.WriteLine(TraceLevel.Error, "Database", "Transactions deadlocked:");
						m_command = "failed to acquire a lock for " + name;
						DoDumpState(TraceLevel.Error);
						throw;
					}
				}
				catch (Exception e)
				{
					Log.WriteLine(TraceLevel.Info, "Database", "{0} trying {1}", e.Message, name);
					UnsafeRollback(name);
					throw;
				}
			}
		}
		
		// In order to ensure that sqlite's lock escalation strategy does not cause deadlocks
		// all calls to Update must be nested inside a transaction. Name is arbitrary, but
		// must match the name used in UnsafeCommit or UnsafeRollback.
		// Note that this method is vulnerable to races so Update(string, Action) should
		// normally be used instead.
		public void UnsafeBegin(string name)
		{
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			Contract.Requires(m_lock == null, "can't nest transaction, old transaction is " + m_lock);
			Contract.Requires(Thread.CurrentThread.ManagedThreadId == m_threadID, m_name + " was used with multiple threads");
			if (m_disposed)
				throw new ObjectDisposedException(GetType().Name);
			
			m_lock = name;
			m_lockTime = DateTime.Now;
			Update("BEGIN IMMEDIATE TRANSACTION");
		}
		
		public void UnsafeCommit(string name)
		{
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			Contract.Requires(name == m_lock, string.Format("m_lock is '{0}' but should be '{1}'", m_lock, name));
			Contract.Requires(Thread.CurrentThread.ManagedThreadId == m_threadID, m_name + " was used with multiple threads");
			if (m_disposed)
				throw new ObjectDisposedException(GetType().Name);
			
			Update("COMMIT TRANSACTION");
			Log.WriteLine(TraceLevel.Verbose, "Database", "{0} transaction took {1:0.000} seconds", m_lock, (DateTime.Now - m_lockTime).TotalMilliseconds/1000.0);
			m_lock = null;
		}
		
		// Will not throw and may be called multiple times.
		public void UnsafeRollback(string name)
		{
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			Contract.Requires(m_lock == null || name == m_lock, string.Format("m_lock is '{0}' but should be '{1}'", m_lock, name));
			Contract.Requires(Thread.CurrentThread.ManagedThreadId == m_threadID, m_name + " was used with multiple threads");
			if (m_disposed)
				throw new ObjectDisposedException(GetType().Name);
			
			if (!m_disposed)
			{
				IntPtr errMesg;
				Error err = sqlite3_exec(m_database, "ROLLBACK TRANSACTION", null, IntPtr.Zero, out errMesg);
				if (err != Error.OK && err != Error.ERROR)			// this is the error returned if ROLLBACK is called twice
					Log.WriteLine(TraceLevel.Warning, "Database", "ROLLBACK returned error {0}", err);
			}
			
			Log.WriteLine(TraceLevel.Warning, "Database", "{0} transaction aborted after {1:0.000} seconds", name, (DateTime.Now - m_lockTime).TotalMilliseconds/1000.0);
			m_lock = null;
			
			GC.KeepAlive(this);
		}
		
		public delegate void HeaderCallback(string[] header);
		public delegate bool RowCallback(string[] row);
		
		// Used for SQL commands which do return a table. If the HeaderCallback is present
		// then it will be called once with the names of the columns in the resulting table.
		// The RowCallback will be called once for each row in the resulting table. If it returns
		// false then row retrieval will be aborted.
		public void Query(string command, HeaderCallback hc, RowCallback rc)
		{
			Contract.Requires(!string.IsNullOrEmpty(command), "command is null or empty");
			Contract.Requires(rc != null, "RowCallback is null");
			Contract.Requires(Thread.CurrentThread.ManagedThreadId == m_threadID, m_name + " was used with multiple threads");
			if (m_disposed)
				throw new ObjectDisposedException(GetType().Name);
			
			Log.WriteLine(TraceLevel.Verbose, "Database", "query: {0}", command);
			Stopwatch timer = null;
			if (Log.IsEnabled(TraceLevel.Verbose, "Database"))
			{
				timer = new Stopwatch();
				timer.Start();
			}
			
			bool calledHeader = false;
			Exception exception = null;
			int count = 0;
			SelectCallback callback = (IntPtr param, int numCols, IntPtr[] values, IntPtr[] names) => 
			{
				Error result = Error.ABORT;
				
				try
				{
					if (!calledHeader && hc != null)
					{
						string[] header = new string[numCols];
						for (int i = 0; i < numCols; ++i)
							header[i] = Marshal.PtrToStringAuto(names[i]);
						hc(header);
						calledHeader = true;
					}
					
					string[] row = new string[numCols];
					for (int i = 0; i < numCols; ++i)
					{
						Contract.Assert(values[i] != IntPtr.Zero, "we don't support null values");
						row[i] = Marshal.PtrToStringAuto(values[i]);
					}
					
					++count;
					if (rc(row))
						result = Error.OK;
				}
				catch (Exception e)
				{
					exception = e;
				}
				
				return result;
			};
			
			try
			{
				m_command = command;
				
				// Note that the database uses a reader/writer lock so this will work even if other
				// threads are trying to write to the database. Also note that this is why we don't
				// return an object which users then use to read rows as ADO.NET does: we'd
				// either have to read all the rows up front or give clients control of the database
				// reader/writer lock.
				IntPtr errMesg = IntPtr.Zero;
				Error err = sqlite3_exec(m_database, command, callback, IntPtr.Zero, out errMesg);
					
				if (timer != null)
					Log.WriteLine(TraceLevel.Verbose, "Database", "query took {0:0.000} secs and {1} rows were processed", timer.ElapsedMilliseconds/1000.0, count);
				
				if (err != Error.OK && err != Error.ABORT)
					DoRaiseError(command, err, errMesg);
				if (exception != null)
				{
					string mesg = string.Format("Failed to execute '{0}'", command);
					throw new DatabaseException(mesg, exception);
				}
			}
			finally
			{
				m_command = null;
			}
			
			GC.KeepAlive(this);
		}
		
		// Note that this should not be used from the main thread if there is a chance that
		// many rows may be returned.
		public string[][] QueryRows(string command)
		{
			Contract.Requires(!string.IsNullOrEmpty(command), "command is null or empty");
			if (m_disposed)
				throw new ObjectDisposedException(GetType().Name);
			
			var rows = new List<string[]>();
			Database.RowCallback rc = (r) => {rows.Add(r); return true;};
			
			Query(command, null, rc);
			
			return rows.ToArray();
		}
		
		//  The attribute names will be those which are listed in the SELECT clause of the command.
		public NamedRows QueryNamedRows(string command)
		{
			Contract.Requires(!string.IsNullOrEmpty(command), "command is null or empty");
			if (m_disposed)
				throw new ObjectDisposedException(GetType().Name);
			
			var rows = new List<string[]>();
			Database.RowCallback rc = (r) => {rows.Add(r); return true;};
			
			Query(command, null, rc);
			
			return new NamedRows(DoGetNames(command), rows.ToArray());
		}
		
		// TODO: 
		// probably want an async BeginQuery method
		// may want to support linq, see:
		//    http://weblogs.asp.net/mehfuzh/archive/2007/10/04/writing-custom-linq-provider.aspx
		//    http://msdn.microsoft.com/en-us/library/bb546158.aspx
		//    http://dotnetslackers.com/articles/csharp/CreatingCustomLINQProviderUsingLinqExtender.aspx
		
		public void Dispose()
		{
			DoDispose(true);
			GC.SuppressFinalize(this);
		}
		
		private void DoDispose(bool disposing)
		{
			if (!m_disposed)
			{
				if (m_database != IntPtr.Zero)
					Unused.Value = sqlite3_close(m_database);
				
				m_database = IntPtr.Zero;
				m_disposed = true;
			}
		}
		
		#region Private Methods
		private void DoRaiseError(string command, Error err, IntPtr errMesg)
		{
			string mesg = string.Format("Failed to execute '{0}'", command);
			
			if (errMesg != IntPtr.Zero)
			{
				mesg += string.Format(" ({0})", Marshal.PtrToStringAuto(errMesg));
				sqlite3_free(errMesg);
			}
			else
				mesg += string.Format(" ({0})", err);
				
			throw (err != Error.BUSY ? new DatabaseException(mesg) : new DatabaseLockedException(mesg));
		}
		
		private Dictionary<string, int> DoGetNames(string command)
		{
			int length = 0;
			int i = DoFind(command, ref length, "SELECT DISTINCT ", "SELECT ");
			Contract.Assert(i >= 0, "couldn't find select clause in " + command);
			i += length;
			
			int j = DoFind(command, ref length, "FROM ", "WHERE ", "GROUP ");
			Contract.Assert(j >= 0, "couldn't find from, where, or group clause in " + command);
			Contract.Assert(i < j, "clauses are in the wrong order");
			
			string text = command.Substring(i, j - i).TrimAll();
			string[] names = text.Split(',');
			
			var result = new Dictionary<string, int>();
			for (int k = 0; k < names.Length; ++k)
			{
				result.Add(names[k], k);
			}
			
			return result;
		}
		
		private int DoFind(string text, ref int length, params string[] names)
		{
			foreach (string name in names)
			{
				int i = text.IndexOf(name);
				if (i > 0)
				{
					length = name.Length;
					return i;
				}
			}
			
			return -1;
		}
		
		[Conditional("DEBUG")]
		private static void DoDumpState(TraceLevel level)
		{
			lock (ms_mutex)
			{
				foreach (WeakReference wr in ms_connections)
				{
					Database db = wr.Target as Database;
					if (db != null && db.m_database != IntPtr.Zero)
					{
						if (db.m_lock != null)
							Log.WriteLine(level, "Database", "Thread {0} {1} is locked for {2} at {3}", db.m_threadID, db.m_name, db.m_lock, db.m_lockTime);
						else if (db.m_command != null)
							Log.WriteLine(level, "Database", "Thread {0} {1} {2}", db.m_threadID, db.m_name, db.m_command);
						else
							Log.WriteLine(level, "Database", "Thread {0} {1}", db.m_threadID, db.m_name);
					}
				}
			}
		}
		
		[Conditional("DEBUG")]
		private static void DoSaveThis(Database db)
		{
			lock (ms_mutex)
			{
				ms_connections.Add(new WeakReference(db));
			}
		}
		#endregion
		
		#region P/Invokes
		private delegate Error SelectCallback(
			IntPtr param,
			int numCols,
			[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] IntPtr[] values,
			[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] IntPtr[] names);
		
		[Flags]
		[Serializable]
		private enum OpenFlags : int
		{
			READONLY = 0x00000001,
			READWRITE = 0x00000002,
			CREATE = 0x00000004,
			DELETEONCLOSE = 0x00000008,
			EXCLUSIVE = 0x00000010,
			MAIN_DB = 0x00000100,
			TEMP_DB = 0x00000200,
			TRANSIENT_DB = 0x00000400,
			MAIN_JOURNAL = 0x00000800,
			TEMP_JOURNAL = 0x00001000,
			SUBJOURNAL = 0x00002000,
			MASTER_JOURNAL = 0x00004000,
			NOMUTEX = 0x00008000,
			FULLMUTEX = 0x00010000,
		}
		
		[Serializable]
		private enum Error : int
		{
			OK = 0,   /* Successful result */
			ERROR = 1,   /* SQL error or missing database */
			INTERNAL = 2,   /* Internal logic error in SQLite */
			PERM = 3,   /* Access permission denied */
			ABORT = 4,   /* Callback routine requested an abort */
			BUSY = 5,   /* The database file is locked */
			LOCKED = 6,   /* A table in the database is locked */
			NOMEM = 7,   /* A malloc() failed */
			READONLY = 8,   /* Attempt to write a readonly database */
			INTERRUPT = 9,   /* Operation terminated by sqlite3_interrupt()*/
			IOERR = 10,   /* Some kind of disk I/O error occurred */
			CORRUPT = 11,   /* The database disk image is malformed */
			NOTFOUND = 12,   /* NOT USED. Table or record not found */
			FULL = 13,   /* Insertion failed because database is full */
			CANTOPEN = 14,   /* Unable to open the database file */
			PROTOCOL = 15,   /* NOT USED. Database lock protocol error */
			EMPTY = 16,   /* Database is empty */
			SCHEMA = 17,   /* The database schema changed */
			TOOBIG = 18,   /* String or BLOB exceeds size limit */
			CONSTRAINT = 19,   /* Abort due to constraint violation */
			MISMATCH = 20,   /* Data type mismatch */
			MISUSE = 21,   /* Library used incorrectly */
			NOLFS = 22,   /* Uses OS features not supported on host */
			AUTH = 23,   /* Authorization denied */
			FORMAT = 24,   /* Auxiliary database format error */
			RANGE = 25,   /* = 2,nd parameter to sqlite3_bind out of range */
			NOTADB = 26,   /* File opened that is not a database file */
			ROW = 100,  /* sqlite3_step() has another row ready */
			DONE = 101,  /* sqlite3_step() has finished executing */
		}
		
		[DllImport("sqlite3")]
		private static extern int sqlite3_busy_timeout(IntPtr db, int ms);
		
		[DllImport("sqlite3")]
		private static extern Error sqlite3_close(IntPtr db);
		
		[DllImport("sqlite3")]
		private static extern Error sqlite3_exec(IntPtr db, string sql, SelectCallback callback, IntPtr param, out IntPtr errMsg);
		
		[DllImport("sqlite3")]
		private static extern void sqlite3_free(IntPtr buffer);
		
		[DllImport("sqlite3")]
		private static extern Error sqlite3_open_v2(string fileName, out IntPtr db, OpenFlags flags, IntPtr module);
		
		[DllImport("sqlite3")]
		private static extern int sqlite3_threadsafe();
		#endregion
				
		#region Fields
		private IntPtr m_database;
		private bool m_disposed;
		private string m_lock;
		private DateTime m_lockTime;
		private string m_name;
		private string m_command;
		private int m_threadID;

#if DEBUG
		private static object ms_mutex = new object();
		private static List<WeakReference> ms_connections = new List<WeakReference>();
#endif
		#endregion
	}
}
