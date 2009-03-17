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

#if TEST
using NUnit.Framework;
using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

[TestFixture]
public sealed class DatabaseTest
{	
	[TestFixtureSetUp]
	public void Init()
	{
		AssertListener.Install();
	}
	
	[Test]
	public void Basics()
	{
		string path = Path.GetTempFileName();
		using (var database = new Database(path))
		{
			database.Begin("test");
			database.Update("CREATE TABLE People(id INTEGER PRIMARY KEY, first_name, last_name, city)");
	
			database.Update("INSERT INTO People VALUES (1, 'joe', 'bob', 'houston')");
			database.Update("INSERT INTO People VALUES (2, 'fred', 'hansen', 'atlanta')");
			database.Update("INSERT INTO People VALUES (3, 'ted', 'bundy', 'houston')");
			database.Commit("test");
			
			string[] header = null;
			List<string[]> rows = new List<string[]>();
			Database.HeaderCallback hc = (h) => {header = h;};
			Database.RowCallback rc = (r) => {rows.Add(r); return true;};
			
			database.Query("SELECT first_name, last_name FROM People WHERE city='houston'", hc, rc);
			
			Assert.AreEqual(2, header.Length);
			Assert.AreEqual("first_name", header[0]);
			Assert.AreEqual("last_name", header[1]);
			
			Assert.AreEqual(2, rows.Count);
			Assert.AreEqual(2, rows[0].Length);
			Assert.AreEqual(2, rows[1].Length);
			
			rows.Sort((lhs, rhs) => lhs[0].CompareTo(rhs[0]));
			Assert.AreEqual("joe", rows[0][0]);
			Assert.AreEqual("bob", rows[0][1]);
			Assert.AreEqual("ted", rows[1][0]);
			Assert.AreEqual("bundy", rows[1][1]);
		}
	}
	
	[Test]
	public void Commit()
	{
		string path = Path.GetTempFileName();
		using (var database = new Database(path))
		{
			database.Begin("test");
			database.Update("CREATE TABLE People(id INTEGER PRIMARY KEY, first_name, last_name, city)");
			
			database.Update("INSERT INTO People VALUES (1, 'joe', 'bob', 'houston')");
			database.Update("INSERT INTO People VALUES (2, 'fred', 'hansen', 'atlanta')");
			database.Update("INSERT INTO People VALUES (3, 'ted', 'bundy', 'houston')");
			database.Commit("test");
			
			List<string[]> rows = new List<string[]>();
			Database.RowCallback rc = (r) => {rows.Add(r); return false;};
			
			database.Query("SELECT first_name, last_name FROM People WHERE city='houston'", null, rc);
			
			Assert.AreEqual(1, rows.Count);
			Assert.AreEqual(2, rows[0].Length);
			Assert.IsTrue((rows[0][0] == "joe" && rows[0][1] == "bob") || (rows[0][0] == "ted" && rows[0][1] == "bundy"), "got " + rows[0][0] + rows[0][1]);
		}
	}
	
	[Test]
	public void Throw()
	{
		string path = Path.GetTempFileName();
		using (var database = new Database(path))
		{
			database.Begin("test");
			database.Update("CREATE TABLE People(id INTEGER PRIMARY KEY, first_name, last_name, city)");
			
			database.Update("INSERT INTO People VALUES (1, 'joe', 'bob', 'houston')");
			database.Update("INSERT INTO People VALUES (2, 'fred', 'hansen', 'atlanta')");
			database.Update("INSERT INTO People VALUES (3, 'ted', 'bundy', 'houston')");
			database.Commit("test");
			
			List<string[]> rows = new List<string[]>();
			Database.RowCallback rc = (r) => {rows.Add(r); throw new Exception("oops");};
			
			try
			{
				database.Query("SELECT first_name, last_name FROM People WHERE city='houston'", null, rc);
				Assert.Fail("should have gotten an exception");
			}
			catch (DatabaseException e)
			{
				Assert.AreEqual(1, rows.Count);
				Assert.AreEqual(2, rows[0].Length);
				Assert.IsTrue((rows[0][0] == "joe" && rows[0][1] == "bob") || (rows[0][0] == "ted" && rows[0][1] == "bundy"), "got " + rows[0][0] + rows[0][1]);
			
				Assert.IsNotNull(e.InnerException);
				Assert.AreEqual("oops", e.InnerException.Message);
			}
		}
	}
	
	[Test]
	public void MultipleRollback()
	{
		string path = Path.GetTempFileName();
		using (var database = new Database(path))
		{
			database.Begin("test");
			database.Update("CREATE TABLE People(id INTEGER PRIMARY KEY, first_name, last_name, city)");
			
			database.Update("INSERT INTO People VALUES (1, 'joe', 'bob', 'houston')");
			database.Update("INSERT INTO People VALUES (2, 'fred', 'hansen', 'atlanta')");
			database.Update("INSERT INTO People VALUES (3, 'ted', 'bundy', 'houston')");
			database.Rollback("test");
			database.Rollback("test");
		}
	}
}
#endif	// TEST
