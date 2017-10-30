@echo off
SETLOCAL

SET CLASSPATH=.;%CLASSPATH%
java org.antlr.v4.gui.TestRig %*

ENDLOCAL