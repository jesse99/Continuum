#!/usr/bin/python
import fileinput
import sys

# Title case the first word on each line of the selection.
def processLine(line):
	for index in xrange(0, len(line)):
		if not line[index].isspace():
			break
			
	if index < len(line):
		sys.stdout.write(line[0:index])
		sys.stdout.write(line[index:].capitalize())
	else:
		sys.stdout.write(line.capitalize())

for line in fileinput.input():
	processLine(line)
