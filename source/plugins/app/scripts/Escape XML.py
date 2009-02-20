#!/usr/bin/python
import fileinput
import sys

def processLine(line):
	line = line.replace("&", "&amp;")
	line = line.replace("<", "&lt;")
	line = line.replace(">", "&gt;")
	line = line.replace("\"", "&quot;")
	line = line.replace("'", "&apos;")
	sys.stdout.write(line)

for line in fileinput.input():
	processLine(line)
