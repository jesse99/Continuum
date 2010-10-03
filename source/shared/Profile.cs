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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Shared
{
	// Simple profiler, especially useful on OS X where mono's profiler doesn't work
	// due to the lack of addr2line.
	public static class Profile
	{
		// Call this for each task you want to track. Note that there can be
		/// only one root task.
		[Conditional("PROFILE")]
		public static void Start(string name)
		{
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			
			if (ms_current == null)
			{
				Contract.Assert(ms_root == null, "Start was called after the root task was stopped");
				
				ms_root = new Task(null, name);
				ms_current = ms_root;
			}
			else
			{
				Task task;
				if (!ms_current.SubTasks.TryGetValue(name, out task))
				{
					task = new Task(ms_current, name);
					ms_current.SubTasks.Add(name, task);
				}
				else
				{
					Contract.Assert(task.StartTime == DateTime.MinValue, "can't nest the same task");
					task.StartTime = DateTime.Now;
					task.Count += 1;
				}
				ms_current = task;
			}
		}
		
		// Call this when the task ends. Name should match the name passed to start. 
		// If exceptions are expected use a finally block to ensure Stop is called.
		[Conditional("PROFILE")]
		public static void Stop(string name)
		{
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			Contract.Requires(ms_current != null, "Start wasn't called");
			Contract.Requires(ms_current.Name == name, name + " doesn't match the last task started which was " + ms_current.Name);
			
			ms_current.Elapsed += DateTime.Now - ms_current.StartTime;
			ms_current.StartTime = DateTime.MinValue;
			
			ms_current = ms_current.Parent;
		}
		
		// Returns a string with formatted results. Something like:
		// Task				Total		Task Only
		// App					30 secs		1 sec
		//    Analyze			24 secs		4 secs
		// 	  Method Rules	15 secs		15 secs
		// 	  Type Rules	5 secs		5 secs
		//    LoadRules		5 secs		5 secs
		//    Report			1 sec		1 sec
		public static string GetResults()
		{
			Contract.Requires(ms_root != null, "Start was never called");
			
			List<Result> results = new List<Result>();
			int maxName = DoGetResults(ms_root, 0, results) + 1;
			
			StringBuilder builder = new StringBuilder();
			
			// hierarchal results
#if true
			builder.Append("Task".PadRight(maxName));
			builder.Append("Total".PadRight(10));
			builder.AppendLine("Task Only");
			
			foreach (Result result in results)
			{
				builder.Append(result.Name.PadRight(maxName));
				builder.Append(result.Total.PadRight(10));
				builder.AppendLine(result.Only);
			}
			builder.AppendLine();
#endif
			
			// flat results
			Dictionary<string, Total> totals = new Dictionary<string, Total>();
			maxName = DoGetTotals(ms_root, totals);
			
			builder.Append("Task".PadRight(maxName));
			builder.Append("Total".PadRight(10));
			builder.AppendLine("Count");
			
			var st = new List<KeyValuePair<string, Total>>(totals);
			st.Sort((lhs, rhs) => rhs.Value.Elapsed.CompareTo(lhs.Value.Elapsed));
			
			foreach (KeyValuePair<string, Total> entry in st)
			{
				builder.Append(entry.Key.PadRight(maxName));
				builder.Append(entry.Value.Elapsed.TotalSeconds.ToString("00.###").PadRight(10));
				builder.AppendLine(entry.Value.Count.ToString());
			}
			
			builder.AppendLine();
			builder.AppendLine("All times are in seconds.");
			
			return builder.ToString();
		}
		
		#region Private methods
		private static int DoGetTotals(Task task, Dictionary<string, Total> totals)
		{
			int maxLen = task.Name.Length;
			
			if (totals.ContainsKey(task.Name))
			{
				totals[task.Name].Elapsed = totals[task.Name].Elapsed + task.Elapsed;
				totals[task.Name].Count = totals[task.Name].Count + task.Count;
			}
			else
				totals[task.Name] = new Total(task.Elapsed, task.Count);
				
			foreach (Task sub in task.SubTasks.Values)	
			{
				int len = DoGetTotals(sub, totals);
				maxLen = Math.Max(maxLen, len);
			}
			
			return maxLen;
		}
		
		private static int DoGetResults(Task task, int indent, List<Result> results)
		{
			// Get the task result,
			Result result = new Result();
			
			result.Name = new string(' ', 3 * indent) + task.Name;
			result.Total = task.Elapsed.TotalSeconds.ToString("00.###");
			int maxName = result.Name.Length;
			
			TimeSpan subTime = TimeSpan.Zero;
			foreach (Task sub in task.SubTasks.Values)	
			{
				subTime += sub.Elapsed;
			}
			result.Only = (task.Elapsed - subTime).TotalSeconds.ToString("00.###");
			
			results.Add(result);
			
			// and the subtask results.
			List<Task> tasks = new List<Task>(task.SubTasks.Values);
			tasks.Sort((lhs, rhs) => rhs.Elapsed.CompareTo(lhs.Elapsed));
			
			foreach (Task sub in tasks)
			{
				int len = DoGetResults(sub, indent + 1, results);
				maxName = Math.Max(maxName, len);
			}
			
			return maxName;
		}
		#endregion
		
		#region Private types
		private sealed class Total
		{
			public TimeSpan Elapsed;
			public int Count;
			
			public Total(TimeSpan elapsed, int count)
			{
				Elapsed = elapsed;
				Count = count;
			}
		}
		
		private struct Result : IEquatable<Result>
		{
			public string Name;
			public string Total;
			public string Only;
			
			public bool Equals(Result rhs)
			{
				return this == rhs;
			}
			
			public override bool Equals(object rhsObj)
			{
				if (rhsObj == null)                        // objects may be null
					return false;
				
				if (GetType() != rhsObj.GetType())
					return false;
				
				Result rhs = (Result) rhsObj;
				return this == rhs;
			}
			
			public static bool operator==(Result lhs, Result rhs)
			{
				return lhs.Name == rhs.Name && lhs.Total == rhs.Total && lhs.Only == rhs.Only;
			}
			
			public static bool operator!=(Result lhs, Result rhs)
			{
				return !(lhs == rhs);
			}
			
			public override int GetHashCode()
			{
				int hash;
				
				unchecked
				{
					hash = Name.GetHashCode() + Total.GetHashCode() + Only.GetHashCode();
				}
				
				return hash;
			}
		}
		
		private sealed class Task				// note that this has to be a class or we get cycles in the object layout
		{
			public readonly Task Parent;
			public readonly string Name;
			public Dictionary<string, Profile.Task> SubTasks;
			public TimeSpan Elapsed;				// total elapsed time for this task (and its subtasks)
			public DateTime StartTime;
			public int Count;
			
			public Task(Task parent, string name)
			{
				Parent = parent;
				Name = name;
				SubTasks = new Dictionary<string, Profile.Task>();
				StartTime = DateTime.Now;
				Count = 1;
			}
		}
		#endregion
		
		#region Fields
		static private Task ms_root;
		static private Task ms_current;
		#endregion
	}
}
