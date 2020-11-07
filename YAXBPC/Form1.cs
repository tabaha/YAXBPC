﻿/*
   Copyright 2012 - 2016 © Nguyen Hung Quy (dreamer2908)

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
 * */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Linq;

namespace YAXBPC {
  public partial class frmMain : Form {
    public frmMain() {
      InitializeComponent();

      // Check OS
      runningInWindows = isMSWindowsEnv();

      // Load settings
      programPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
      settingsFile = Path.ChangeExtension(programPath, "ini");
      programDir = Path.GetDirectoryName(programPath);
      settings = new Database(settingsFile);

      try {
        if (settings.Load())
          loadSettings();
      }
      catch {
        //nothing
      }

      run64bitxdelta = chbRun64bitxdelta3.Checked;
      dist64bitxdelta = chbDist64bitxdelta3.Checked;
      funnyMode = chbFunnyMode.Checked;

      // debug mode
      debugMode = false;
      forceUnicodeMode = false;
    }

    #region Global variables & type defs

    // runtime environment & debug mode
    bool debugMode = false;
    bool forceUnicodeMode = false;
    string settingsFile = "";
    string programPath = "";
    string programDir = "";
    bool runningInWindows = false;

    Database settings;
    string quote = '"'.ToString();
    Int32 outputPlace = 0;
    Int32 currentlySelectedTab = 0;
    Int32 currentlyBeingEditedJob = 0;
    bool tryDetectingEpisodeNumber = false;
    bool useCustomParamenter = false;
    string customParamenter = "";
    bool run64bitxdelta = false;
    bool dist64bitxdelta = false;
    bool funnyMode = false;
    bool batchProcessingMode = false;
    bool addNewPatchToApplyAllScripts = false;
    bool alwaysCopySourceFiles = false;
    bool skipAlternativeScripts = false;

    // shared variables for batch processing
    Int32 jobsCount = 0;
    bool thisJobDone = false;
    string sourceFile_currentJob = "";
    string targetFile_currentJob = "";
    string outputDir_currentJob = "";

    public delegate void Int32_Delegate(Int32 index);
    public delegate void String_Delegate(string log);
    public delegate void Void_Delegate();

    struct EpNum {
      public string text;// = "";
      public Int32 number;// = -1;
      public Int32 length;// = 0;
      public Int32 priority;// = 0; 
    }

    #endregion

    #region Delegate methods

    private void AddText2Log(string log) {
      if (this.rtbLog.InvokeRequired) {
        String_Delegate d = new String_Delegate(rtbLog.AppendText);
        this.Invoke
            (d, new object[] { log });
      }
      else {
        rtbLog.AppendText(log);
      }
    }

    private void AddText2ApplyLog(string log) {
      if (this.rtbLog.InvokeRequired) {
        String_Delegate d = new String_Delegate(rtbApplyLog.AppendText);
        this.Invoke
            (d, new object[] { log });
      }
      else {
        rtbApplyLog.AppendText(log);
      }
    }

    private bool copyTask(int index) {
      thisJobDone = false;
      if (this.listView1.InvokeRequired) {
        Int32_Delegate d = new Int32_Delegate(_CopyTask);
        this.Invoke
            (d, new object[] { index });
      }
      else {
        _CopyTask(index);
      }
      return !thisJobDone;
    }

    private void _CopyTask(int index) {
      if (listView1.Items[index].ImageIndex != 0) {
        thisJobDone = false;
        sourceFile_currentJob = listView1.Items[index].SubItems[0].Text;
        targetFile_currentJob = listView1.Items[index].SubItems[1].Text;
        outputDir_currentJob = listView1.Items[index].SubItems[2].Text;
        listView1.Items[index].ImageIndex = 1;
      }
      else thisJobDone = true;
    }

    private void setTaskFinished(int index) {
      if (this.listView1.InvokeRequired) {
        Int32_Delegate d = new Int32_Delegate(_SetTaskFinished);
        this.Invoke
            (d, new object[] { index });
      }
      else {
        _SetTaskFinished(index);
      }
    }

    private void _SetTaskFinished(int index) {
      listView1.Items[index].ImageIndex = 0;
    }

    private void getNumOfTask() {
      if (this.listView1.InvokeRequired) {
        Void_Delegate d = new Void_Delegate(_GetNumOfTask);
        this.Invoke
            (d, new object[] { });
      }
      else {
        _GetNumOfTask();
      }
    }

    private void _GetNumOfTask() {
      jobsCount = listView1.Items.Count;
    }

    #endregion

    #region Core methods

    private bool isMSWindowsEnv() {
      System.OperatingSystem osInfo = System.Environment.OSVersion;
      switch (osInfo.Platform) {
        case System.PlatformID.Win32Windows: {
            // Windows 95, Windows 98, Windows 98 Second Edition, or Windows Me.
            return true;
          }
        case System.PlatformID.Win32NT: {
            // Windows NT 3.51, Windows NT 4.0, Windows 2000, Windows XP, Windows Vista, Windows 7, Windows 8, or later
            return true;
          }
        default: return false;
      }
    }

    private bool stringContainsNonASCIIChar(string inputString) {
      if (forceUnicodeMode) return true;
      // Strip non-ASCII characters from the string using Regex
      string onlyAscii = Regex.Replace(inputString, @"[^\u0000-\u007F]", string.Empty);
      // If inputstring is different from onlyAscii then it contains non-ascii char
      return (inputString != onlyAscii);
    }

    private bool stringContainsNon1252Char(string inputString) {
      // check if string contains any character not in windows-1252
      // by encoding to windows-1252 byte array and decode back to unicode
      // if something changes then true
      Encoding wind1252 = Encoding.GetEncoding(1252);
      return (inputString != wind1252.GetString(wind1252.GetBytes(inputString)));
    }

    private void generateOutputDirName(string sourceFile, string targetFile, int outputPlace, string customOutputDir) {
      string episodeNumber = "";
      string outputDir = "";
      string baseOutputDir = "";
      string name2Detect = "";

      // Generate parent dir
      switch (outputPlace) {
        case 0:
          if (File.Exists(sourceFile)) {
            baseOutputDir = Path.GetDirectoryName(sourceFile);
            name2Detect = Path.GetFileName(sourceFile);
          }
          break;
        case 1:
          if (File.Exists(targetFile)) {
            baseOutputDir = Path.GetDirectoryName(targetFile);
            name2Detect = Path.GetFileName(targetFile);
          }
          break;
        case 2:
          baseOutputDir = customOutputDir;
          if (File.Exists(sourceFile)) name2Detect = Path.GetFileName(sourceFile);
          break;
      }

      // Generate the name
      if (!chbNewAutoName.Checked) {
        if (chbDetEpNum.Checked) {
          episodeNumber = getEpisodeNumber(name2Detect);
          outputDir = Path.Combine(baseOutputDir, ((episodeNumber.Length > 0) ? episodeNumber : "patch"));
        }
        else {
          outputDir = Path.Combine(baseOutputDir, "patch");
        }
      }
      else if (sourceFile.Length > 0 && targetFile.Length > 0) {
        // Already disabled, but just for sure
        if (chbDetEpNum.Checked) {
          episodeNumber = getEpisodeNumber(name2Detect);
          outputDir = Path.Combine(baseOutputDir, ((episodeNumber.Length > 0) ? episodeNumber : "patch"));
        }
        else {
          outputDir = Path.Combine(baseOutputDir, "patch");
        }
      }

      txtOutputDir.Text = outputDir;
    }

    // I don't remember how I did this, but apparently it works well enough
    private string getEpisodeNumber(string sourceFile) {
      string episodeNumber = "";
      int[] pos = new int[sourceFile.Length];
      string SP = "_-[] v.";
      string[] SPC = { "_", "-", " ", "[", "]" };
      int length = sourceFile.Length;

      EpNum[] text2Parse = new EpNum[length];
      int found = 0;
      int count = 0;
      for (int cur = 0; cur < length; cur++) {
        if (SP.Contains(sourceFile.Substring(cur, 1))) {
          string strFound = sourceFile.Substring(found, cur - found + 1);
          int _length = strFound.Length;
          int _prio = 0;
          int _number = 0;
          if (length < 1 || SP.Contains(strFound)) continue;
          bool valid = int.TryParse(strFound.Substring(1, _length - 2), out _number);

          if (valid) {
            // Set its priority accoding to the patern
            if (strFound.Substring(_length - 1, 1) == "v") _prio += 1; // 02v (2) will get higher priority
            else {
              if (strFound.Substring(_length - 1, 1) == strFound.Substring(0, 1)) // _02_ too
              {
                if (strFound.Substring(0, 1) == "v") _prio -= 1; // but not v02v 
                else _prio += 1;
              }
            }
            //else _prio -= 1; // No reason to reduce its priotity

            if (found > 0) { if (_length == 4) _prio += 1; } else { if (_length == 3) _prio += 1; } // An episode number usually contains 2 characters

            text2Parse[count] = new EpNum();
            text2Parse[count].text = strFound;
            text2Parse[count].length = _length - 2;

            if (found == 0) text2Parse[count].length = _length - 1;

            text2Parse[count].priority = _prio;
            text2Parse[count].number = _number;

            count++;
          }
          found = cur;
        }
      }

      if (count > 0) {
        if (count > 1) {
          // Use priority to choose the best number
          int right = 0;
          int max = -10;
          for (int i = 0; i < count; i++) {
            if (text2Parse[i].priority > max) {
              right = i;
              max = text2Parse[i].priority;
            }
          }
          text2Parse[0] = text2Parse[right]; //Copy to the beginning
        }
        episodeNumber = text2Parse[0].number.ToString(); // Take the first one
        while (episodeNumber.Length < text2Parse[0].length) episodeNumber = "0" + episodeNumber; // Add "0" to get "01", and "02" instead of "1", and "2"
      }
      return episodeNumber;
    }

    // Temporary solution. It looks dirty to me, really >_>
    private string killAOption(string options) {
      string result = options;
      if (options.Contains("-A=")) {
        int i = options.IndexOf("-A=");
        result = options.Substring(0, i);
        string tmp = options.Substring(i + 3);
        if (tmp.Length > 0) {
          if (tmp[0] == '"') {
            // kill string in quotes
            if (tmp.Length > 1) {
              int quote = tmp.IndexOf('"', 1);
              if (quote >= 1 && quote + 1 < tmp.Length) result += " " + tmp.Substring(quote + 1);
            }
          }
          else {
            // kill string before the first white space. If no space found, kill all
            tmp = tmp.TrimEnd();
            int space = tmp.IndexOf(' ');
            if (space >= 0 && space + 1 < tmp.Length) {
              result += " " + tmp.Substring(space + 1);
            }
          }
        }
      }
      return result;
    }

    private void createPatch(string _sourceFile, string _targetFile, string _outDir) {
      bool useRelativePath = chbOnlyStoreFileNameInVCDIFF.Checked || stringContainsNonASCIIChar(Path.GetFileName(_sourceFile)) || stringContainsNonASCIIChar(Path.GetFileName(_targetFile));
      string sourceFile = _sourceFile;
      string targetFile = _targetFile;
      string sourceFileName = Path.GetFileName(_sourceFile);
      string targetFileName = Path.GetFileName(_targetFile);
      string outputDir = _outDir;
      string outputVcdiff = Path.Combine(outputDir, "changes.vcdiff");
      bool plsRmTmpSourceFile = false;
      bool plsRmTmpTargetFile = false;

      if (runningInWindows) {
        if (alwaysCopySourceFiles || stringContainsNon1252Char(sourceFile)) {
          AddText2Log("Making a temporary copy of the source file...\n");
          string tmpFname = Guid.NewGuid().ToString() + ".tmp";
          File.Copy(sourceFile, tmpFname, true);
          sourceFile = tmpFname;
          sourceFileName = sourceFileName.Replace("＂", ""); // xdelta3 in Windows doesn't support unicode. full-width quote will become normal quote when xdelta3 receives it, so problems will arise. Alter the filename in vcdiff header a bit to work around this. This filename is for decoration purpose so no problems.
          plsRmTmpSourceFile = true;
        }
        if (alwaysCopySourceFiles || stringContainsNon1252Char(targetFile)) {
          AddText2Log("Making a temporary copy of the target file...\n");
          string tmpFname = Guid.NewGuid().ToString() + ".tmp";
          File.Copy(targetFile, tmpFname, true);
          targetFile = tmpFname;
          targetFileName = targetFileName.Replace("＂", "");
          plsRmTmpTargetFile = true;
        }
      }

      Process xdelta = new Process();
      if (runningInWindows && run64bitxdelta) xdelta.StartInfo.FileName = "xdelta3.x86_64.exe";
      else xdelta.StartInfo.FileName = "xdelta3"; // Works with xdelta3.exe and xdelta3 package, doesn't work with ./xdelta3
      xdelta.StartInfo.CreateNoWindow = true;
      xdelta.StartInfo.UseShellExecute = false;

      // Capture xdelta3 console output. It can be useful
      xdelta.StartInfo.RedirectStandardOutput = true;
      xdelta.StartInfo.RedirectStandardError = true;
      // hookup the eventhandlers to capture the data that is received
      var sb = new StringBuilder();
      xdelta.OutputDataReceived += (sender, args) => sb.AppendLine(args.Data);
      xdelta.ErrorDataReceived += (sender, args) => sb.AppendLine(args.Data);

      if (!useCustomParamenter) {
        xdelta.StartInfo.Arguments = "-f -e -s %source% %patched% %vcdiff%";
      }
      else { xdelta.StartInfo.Arguments = customParamenter; }

      xdelta.StartInfo.Arguments = "-D " + xdelta.StartInfo.Arguments; // disable external decompression in case of archives like gz. Can be overriden by later paramenters. Doesn't really matter as this app targets fansubbed mkv files.

      xdelta.StartInfo.Arguments = xdelta.StartInfo.Arguments.Replace("%source%", quote + sourceFile + quote).Replace("%patched%", quote + targetFile + quote).Replace("%vcdiff%", quote + outputVcdiff + quote);

      // Needs to kill -A option first 'cause xdelta3 always takes the last one, and all options must come before filenames
      xdelta.StartInfo.Arguments = killAOption(xdelta.StartInfo.Arguments);
      // See main_set_appheader in xdelta3-main.h for how xdelta3 stores filenames
      if (funnyMode) {
        xdelta.StartInfo.Arguments = "-A=\"" + "**STAR**STAR**STAR** Why am I suddenly seeing stars?" + "//" + "???!@#$%^&*() Either use the provided scripts or type the full command. Thx." + "/\" " + xdelta.StartInfo.Arguments;
      }
      else if (useRelativePath) {
        xdelta.StartInfo.Arguments = "-A=\"" + targetFileName + "//" + sourceFileName + "/\" " + xdelta.StartInfo.Arguments;
      }

      if (debugMode) MessageBox.Show(xdelta.StartInfo.Arguments);

      xdelta.Start();
      // Capture xdelta3 console output
      xdelta.BeginOutputReadLine();
      xdelta.BeginErrorReadLine();

      xdelta.WaitForExit();
      if (debugMode) MessageBox.Show(sb.ToString());

      if (plsRmTmpSourceFile) {
        File.Delete(sourceFile);
      }
      if (plsRmTmpTargetFile) {
        File.Delete(targetFile);
      }

      if (xdelta.ExitCode != 0) // I, right here, abuse exception. Feel free to sue me.
      {
        throw new Exception(sb.ToString().Trim());
      }
    }

    private void copyXdeltaBinaries(string outputFolder) {
      string source = Path.Combine(programDir, (dist64bitxdelta) ? "xdelta3.x86_64.exe" : "xdelta3.exe");
      string source2 = Path.Combine(programDir, "xdelta3");
      string source3 = Path.Combine(programDir, "xdelta3.x86_64");
      string source4 = Path.Combine(programDir, "xdelta3_mac");
      string target = Path.Combine(outputFolder, "xdelta3.exe");
      string target2 = Path.Combine(outputFolder, "xdelta3");
      string target3 = Path.Combine(outputFolder, "xdelta3.x86_64");
      string target4 = Path.Combine(outputFolder, "xdelta3_mac");

      File.Copy(source, target, true);
      File.Copy(source2, target2, true);
      File.Copy(source3, target3, true);
      File.Copy(source4, target4, true);
    }

    // # %^& must be escaped. \/<>"*:?| are forbidden in win32 filenames. []()!=,;`' work, so not needed
    private string escapeStringForBatch(string text) {
      string result = "";
      for (int i = 0; i < text.Length; i++) {
        char chr = text[i];
        switch (chr) {
          case '%': result += "%%"; break;
          case '&': result += "^&"; break;
          case '^': result += "^^"; break;
          default: result += chr; break;
        }
      }

      return result;
    }

    private void createApplyingScripts(string sourceFile, string targetFile, string outputDir) {
      // directly copy these alternative scripts to output dir
      if (!skipAlternativeScripts) {
        string[] alternativeScripts = { "apply_patch_windows_alternative.bat", "apply_patch_mac_alternative.command", "apply_patch_linux_alternative.sh" };
        foreach (string s in alternativeScripts) {
          string source = Path.Combine(programDir, s);
          string target = Path.Combine(outputDir, s);
          File.Copy(source, target, true);
        }
      }

      // Linux & Mac scripts seamlessly work with non-ascii filenames.
      string linuxScript = File.ReadAllText(Path.Combine(programDir, "apply_patch_linux.sh"));
      string macScript = File.ReadAllText(Path.Combine(programDir, "apply_patch_mac.command"));
      string readMe = File.ReadAllText(Path.Combine(programDir, "how_to_apply_this_patch.txt"));

      readMe = readMe.Replace("&sourcefile&", sourceFile).Replace("&targetfile&", targetFile);
      linuxScript = linuxScript.Replace("&sourcefile&", sourceFile.Replace("'", "'\"'\"'")).Replace("&targetfile&", targetFile.Replace("'", "'\"'\"'")); // quote all single quotes as fnames are quoted in single quotes
      macScript = macScript.Replace("&sourcefile&", sourceFile.Replace("'", "'\"'\"'")).Replace("&targetfile&", targetFile.Replace("'", "'\"'\"'")); // just replace ' with '"'"' (single, double, single, double, single)

      // Unified both scripts. Now only apply_patch_windows.bat
      string winScript = File.ReadAllText(Path.Combine(programDir, "apply_patch_windows.bat"));
      // The brand-new PowerShell subscript
      string psScript = File.ReadAllText(Path.Combine(programDir, "subscript1.ps1"));

      winScript = winScript.Replace("&sourcefile&", escapeStringForBatch(sourceFile)).Replace("&targetfile&", escapeStringForBatch(targetFile));
      psScript = psScript.Replace("&sourcefile&", sourceFile.Replace("'", "''")).Replace("&targetfile&", targetFile.Replace("'", "''")); // Any single quote in single-quoted PowerShell string must be doubled (replace ' with '')
      if (stringContainsNonASCIIChar(Path.GetFileName(sourceFile))) {
        winScript = winScript.Replace("set movesourcefile=0", "set movesourcefile=1");
        psScript = psScript.Replace("movesourcefile = 0", "movesourcefile = 1"); // even though PS has native unicode support, calling xdelta3 with unicode paramenters still ends up in failure
      }
      if (stringContainsNonASCIIChar(Path.GetFileName(targetFile))) {
        winScript = winScript.Replace("set movetargetfile=0", "set movetargetfile=1");
        psScript = psScript.Replace("movetargetfile = 0", "movetargetfile = 1");
      }
      if (!(stringContainsNonASCIIChar(Path.GetFileName(sourceFile) + Path.GetFileName(targetFile)))) {
        winScript = winScript.Replace("chcp 65001", "rem chcp 65001");
      }

      // Generate output file paths
      string macPath = Path.Combine(outputDir, "apply_patch_mac.command");
      string winPath = Path.Combine(outputDir, "apply_patch_windows.bat");
      string linuxPath = Path.Combine(outputDir, "apply_patch_linux.sh");
      string readMePath = Path.Combine(outputDir, "how_to_apply_this_patch.txt");
      string psPath = Path.Combine(outputDir, "subscript1.ps1");

      // write outputs
      File.WriteAllText(winPath, winScript);
      File.WriteAllText(linuxPath, linuxScript);
      File.WriteAllText(macPath, macScript);
      File.WriteAllText(readMePath, readMe, Encoding.UTF8);
      File.WriteAllText(psPath, psScript, Encoding.UTF8); // encoding utf-8 with BOM

      // "Apply all" scripts
      if (addNewPatchToApplyAllScripts) {
        DirectoryInfo directoryInfo = Directory.GetParent(outputDir);
        if (directoryInfo == null) {
          throw new Exception("\"Apply all\" scripts can't be created if the output directory is the root directory as they need to be in one level upper."); // I, right here, abuse exception. Feel free to sue me.
        }
        string parentDirPath = directoryInfo.FullName;
        string outputDirName = new DirectoryInfo(outputDir).Name;

        string applyAllWinPath = Path.Combine(parentDirPath, "apply_all_patches_windows.bat");
        string applyAllLinuxPath = Path.Combine(parentDirPath, "apply_all_patches_linux.sh");
        string applyAllMacPath = Path.Combine(parentDirPath, "apply_all_patches_mac.command");

        if (!File.Exists(applyAllLinuxPath)) {
          File.WriteAllText(applyAllLinuxPath, "#!/bin/sh\ncd \"$(cd \"$(dirname \"$0\")\" && pwd)\"");
        }
        if (!File.Exists(applyAllMacPath)) {
          File.WriteAllText(applyAllMacPath, "#!/bin/sh\ncd \"$(cd \"$(dirname \"$0\")\" && pwd)\"");
        }
        if (!File.Exists(applyAllWinPath)) {
          File.WriteAllText(applyAllWinPath, "chdir /d %~dp0\r\n@echo off\r\nsetlocal");
        }
        File.AppendAllText(applyAllWinPath, "\r\ncall \".\\" + outputDirName + "\\apply_patch_windows.bat\""); // use "call blablah.bat"
        File.AppendAllText(applyAllLinuxPath, "\nsh './" + outputDirName + "/apply_patch_linux.sh'");
        File.AppendAllText(applyAllMacPath, "\nsh './" + outputDirName + "/apply_patch_mac.command'");
      }
    }

    private void createPatchBackground_sub(string sourceFile, string targetFile, string outputDir) {
      try {
        AddText2Log("Creating output directory...\n");
        if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
      }
      catch (Exception e) {
        AddText2Log("Task failed: " + e.Message + "\n\n");
        return;
      }

      try {
        AddText2Log("Creating patch...\n");
        createPatch(sourceFile, targetFile, outputDir);
      }
      catch (Exception e) {
        AddText2Log("Task failed: " + e.Message + "\n\n");
        return;
      }

      try {
        AddText2Log("Creating applying scripts...\n");
        createApplyingScripts(Path.GetFileName(sourceFile), Path.GetFileName(targetFile), outputDir);
      }
      catch (Exception e) {
        AddText2Log("Task failed: " + e.Message + "\n\n");
        return;
      }

      try {
        AddText2Log("Copying xdelta3 to output directory...\n");
        copyXdeltaBinaries(outputDir);
      }
      catch (Exception e) {
        AddText2Log("Task failed: " + e.Message + "\n\n");
        return;
      }

      AddText2Log("Done.\n\n");
    }

    private void createPatchBackground(object sender, DoWorkEventArgs e) {
      if (!batchProcessingMode) {
        // Single job
        if (txtSourceFile.Text.Trim().Equals(String.Empty)) AddText2Log("Please specify the source file.\n\n");
        else if (txtTargetFile.Text.Trim().Equals(String.Empty)) AddText2Log("Please specify the target file.\n\n");
        else if (txtOutputDir.Text.Trim().Equals(String.Empty)) AddText2Log("Please specify the output directory.\n\n");
        else createPatchBackground_sub(txtSourceFile.Text, txtTargetFile.Text, txtOutputDir.Text);
      }
      else {
        // Batch processing
        if (currentlySelectedTab == 1) {
          getNumOfTask();
          int old = jobsCount;
          if (jobsCount > 0)
            for (int n = 0; n < jobsCount; n++) {
              if (copyTask(n)) {
                createPatchBackground_sub(sourceFile_currentJob, targetFile_currentJob, outputDir_currentJob);
                setTaskFinished(n);
              }
              getNumOfTask(); // Re-get the number of task(s) in case task(s) added/removed
              if (old != jobsCount) n = 0; // Re-start the queue. Currently, i's safe to assume this, because changing task order is not supported.
            }
        }
      }
    }

    private void applyPatchBackground(object sender, DoWorkEventArgs e) {
      //if (txtApplySource.Text.Trim().Equals(String.Empty)) # input fname is also not a compulsory paramenter if it has been already stored in vcdiff's header
      //{
      //    AddText2ApplyLog("Please specify the source file.\n\n");
      //    return;
      //}
      if (txtApplyVcdiffFile.Text.Trim().Equals(String.Empty)) {
        AddText2ApplyLog("Please specify the delta file.\n\n");
        return;
      }
      //else if (txtApplyOutput.Text.Trim().Equals(String.Empty)) # output fname is not a compulsory paramenter if it has been already stored in vcdiff's header
      //{
      //    AddText2ApplyLog("Please specify the output file.\n\n");
      //    return;
      //}

      string inputFile = txtApplySource.Text;
      try {
        AddText2ApplyLog("Attempting to patch \"" + Path.GetFileName(inputFile) + "\"...\n");
        string result = applyOnePatch(txtApplySource.Text, txtApplyVcdiffFile.Text, txtApplyOutput.Text);
        AddText2ApplyLog(result.Trim() + "\n\n");

      }
      catch (Exception ex) {
        AddText2ApplyLog("Task failed: " + ex.Message.Trim() + "\n\n");
        return;
      }
    }

    private string applyOnePatch(string _sourceFile, string vcdiffFile, string _outputFile) {
      string sourceFile = _sourceFile;
      string outputFile = _outputFile;
      bool plsRmTmpSourceFile = false;
      bool plsMvTmpOutputFile = false;

      if (runningInWindows) {
        if (alwaysCopySourceFiles || stringContainsNon1252Char(sourceFile)) {
          string tmpFname = Guid.NewGuid().ToString() + ".tmp";
          File.Copy(sourceFile, tmpFname, true);
          sourceFile = tmpFname; // ascii-only and no path, so safe. Most likely will end up in YAXBPC program dir.
          plsRmTmpSourceFile = true;
        }
        if (alwaysCopySourceFiles || stringContainsNon1252Char(outputFile)) {
          string tmpFname = Guid.NewGuid().ToString() + ".tmp";
          outputFile = tmpFname;
          plsMvTmpOutputFile = true;
        }
      }

      Process xdelta = new Process();
      if (runningInWindows && run64bitxdelta) xdelta.StartInfo.FileName = "xdelta3.x86_64.exe";
      else xdelta.StartInfo.FileName = "xdelta3"; // Works with xdelta3.exe and xdelta3 package, doesn't work with ./xdelta3
      xdelta.StartInfo.CreateNoWindow = true;
      xdelta.StartInfo.UseShellExecute = false;

      // Capture xdelta3 console output. It can be useful
      xdelta.StartInfo.RedirectStandardOutput = true;
      xdelta.StartInfo.RedirectStandardError = true;
      // hookup the eventhandlers to capture the data that is received
      var sb = new StringBuilder();
      xdelta.OutputDataReceived += (sender, args) => sb.AppendLine(args.Data);
      xdelta.ErrorDataReceived += (sender, args) => sb.AppendLine(args.Data);

      string paramenters = (chbUseCustomXdeltaParamsForApplying.Checked) ? txtCustomXdeltaParamsForApplying.Text : "-d -f -s %source% %vcdiff% %output%";
      xdelta.StartInfo.Arguments = paramenters.Replace("%vcdiff%", quote + vcdiffFile + quote);
      if (outputFile == "" && sourceFile != "") // don't trim it, as filename consisting of all spaces is also valid
      {
        // output is not specified but source is. Handle thing a little differently
        xdelta.StartInfo.Arguments = xdelta.StartInfo.Arguments.Replace("%output%", "");
        xdelta.StartInfo.Arguments = xdelta.StartInfo.Arguments.Replace("%source%", quote + sourceFile + quote);
        xdelta.StartInfo.WorkingDirectory = Path.GetDirectoryName(sourceFile);
      }
      else if (outputFile != "" && sourceFile == "") {
        // output is specified but source is not. Handle thing a little differently
        xdelta.StartInfo.Arguments = xdelta.StartInfo.Arguments.Replace("%output%", quote + outputFile + quote);
        xdelta.StartInfo.Arguments = xdelta.StartInfo.Arguments.Replace("%source%", "").Replace("-s ", ""); // will be improved when a paramenter parser is implemented
        xdelta.StartInfo.WorkingDirectory = Path.GetDirectoryName(vcdiffFile);
      }
      else if (outputFile == "" && sourceFile == "") {
        // both source and output are not specified. Handle thing a little differently
        xdelta.StartInfo.Arguments = xdelta.StartInfo.Arguments.Replace("%output%", "");
        xdelta.StartInfo.Arguments = xdelta.StartInfo.Arguments.Replace("%source%", "").Replace("-s ", ""); // will be improved when a paramenter parser is implemented
        xdelta.StartInfo.WorkingDirectory = Path.GetDirectoryName(vcdiffFile);
      }
      else {
        // normal case: both source and output are specified
        xdelta.StartInfo.Arguments = xdelta.StartInfo.Arguments.Replace("%output%", quote + outputFile + quote);
        xdelta.StartInfo.Arguments = xdelta.StartInfo.Arguments.Replace("%source%", quote + sourceFile + quote);
      }
      if (debugMode) MessageBox.Show(xdelta.StartInfo.WorkingDirectory);

      xdelta.Start();
      // Capture xdelta3 console output
      xdelta.BeginOutputReadLine();
      xdelta.BeginErrorReadLine();

      xdelta.WaitForExit();
      if (debugMode) MessageBox.Show(sb.ToString());

      if (plsRmTmpSourceFile) {
        File.Delete(sourceFile);
      }
      if (plsMvTmpOutputFile) {
        File.Move(outputFile, _outputFile);
      }

      return (xdelta.ExitCode == 0) ? "All OK." : "Error: " + sb.ToString();
    }

    #endregion

    #region Buttons and events

    private void frmMain_FormClosing(object sender, FormClosingEventArgs e) {
      saveSettings();
    }

    private void tabControl1_SelectedIndexChanged(object sender, EventArgs e) {
      currentlySelectedTab = tabControl1.SelectedIndex;
      switch (currentlySelectedTab) {
        case 0: btnStart.Text = "&Create Patch"; break;
        case 1: btnStart.Text = "&Start Processing"; break;
        case 2: btnStart.Text = "&Apply Patch"; break;
        default: btnStart.Text = "&Create Patch"; break;
      }
      batchProcessingMode = (currentlySelectedTab == 1);
    }

    #region File Patcher tab

    private void btnBrowseSourceFile_Click(object sender, EventArgs e) {
      if (ofdFileBrowser.ShowDialog() == DialogResult.OK) {
        txtSourceFile.Text = ofdFileBrowser.FileName;
        generateOutputDirName(txtSourceFile.Text, txtTargetFile.Text, outputPlace, txtDefaultOutDir.Text); // Generate a new output folder
      }
    }

    private void btnBrowseTargetFile_Click(object sender, EventArgs e) {
      if (ofdFileBrowser.ShowDialog() == DialogResult.OK) txtTargetFile.Text = ofdFileBrowser.FileName;

      if (chbNewAutoName.Checked || rdbTargetDir.Checked) {
        generateOutputDirName(txtSourceFile.Text, txtTargetFile.Text, outputPlace, txtDefaultOutDir.Text);  // Generate a new output folder again
      }
    }

    private void btnBrowseOutputDir_Click(object sender, EventArgs e) {
      if (fbdDirBrowser.ShowDialog() == DialogResult.OK) txtOutputDir.Text = fbdDirBrowser.SelectedPath;
    }

    private void txtSourceFile_DragEnter(object sender, DragEventArgs e) {
      if (e.Data.GetDataPresent(DataFormats.FileDrop)) // File only, you faggots!
        e.Effect = DragDropEffects.All;
      else
        e.Effect = DragDropEffects.None;
    }

    private void txtSourceFile_DragDrop(object sender, DragEventArgs e) {
      string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
      txtSourceFile.Text = files[0];
      generateOutputDirName(txtSourceFile.Text, txtTargetFile.Text, outputPlace, txtDefaultOutDir.Text); // generate a new output folder whenever dropping
    }

    private void txtTargetFile_DragDrop(object sender, DragEventArgs e) {
      string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
      txtTargetFile.Text = files[0];
      if (chbNewAutoName.Checked || rdbTargetDir.Checked) {
        generateOutputDirName(txtSourceFile.Text, txtTargetFile.Text, outputPlace, txtDefaultOutDir.Text); // Generate a new output folder and again, whenever a file is dropped
      }
    }

    private void txtTargetFile_DragEnter(object sender, DragEventArgs e) {
      if (e.Data.GetDataPresent(DataFormats.FileDrop)) // File only, you faggots!
        e.Effect = DragDropEffects.All;
      else
        e.Effect = DragDropEffects.None;
    }

    private void txtOutputDir_DragEnter(object sender, DragEventArgs e) {
      if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (Directory.Exists(files[0])) e.Effect = DragDropEffects.All; // Check if the first one is an existing folder
      }
      else
        e.Effect = DragDropEffects.None;
    }

    private void txtOutputDir_DragDrop(object sender, DragEventArgs e) {
      string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
      txtOutputDir.Text = files[0]; // Take the first one
    }

    private void btnSwapSnT_Click(object sender, EventArgs e) {
      string temp = txtSourceFile.Text;
      txtSourceFile.Text = txtTargetFile.Text;
      txtTargetFile.Text = temp;

      if (chbAddTextWhenSwap.Checked && txtOutputDir.Text.Length > 0 && !txtOutputDir.Text.EndsWith(txtAddTextWhenSwap.Text)) txtOutputDir.Text += txtAddTextWhenSwap.Text; // check if already added
      if (chbNewAutoName.Checked) {
        generateOutputDirName(txtSourceFile.Text, txtTargetFile.Text, outputPlace, txtDefaultOutDir.Text); // Again!
      }
    }

    private void btnResetForms_Click(object sender, EventArgs e) {
      if (btnResetForms.Text == "&Reset Forms") {
        txtSourceFile.Text = txtOutputDir.Text = txtTargetFile.Text = "";
      }
      else {
        // Restore buttons
        btnResetForms.Text = "&Reset Forms";
        btnAddEditJob.Text = "&Add to batch";
      }
    }

    private void btnStart_Click(object sender, EventArgs e) {
      switch (currentlySelectedTab) {
        case 0: if (!bgwCreatePatch.IsBusy) bgwCreatePatch.RunWorkerAsync(); break;
        case 1: if (!bgwCreatePatch.IsBusy) bgwCreatePatch.RunWorkerAsync(); break;
        case 2: if (!bgwApplyPatch.IsBusy) bgwApplyPatch.RunWorkerAsync(); break;
        default: if (!bgwCreatePatch.IsBusy) bgwCreatePatch.RunWorkerAsync(); break;
      }
    }

    private void btnExit_Click(object sender, EventArgs e) {
      this.Close();
    }

    private void btnAddEditJob_Click(object sender, EventArgs e) {
      if (btnAddEditJob.Text == "&Add to batch") // Check if this is an edit or a new
      {
        // Add a new job
        if (txtTargetFile.Text.Length > 0 && txtOutputDir.Text.Length > 0 && txtSourceFile.Text.Length > 0) // Validate all fields
        {
          ListViewItem temp = listView1.Items.Add(txtSourceFile.Text); // Add to listview
          temp.SubItems.Add(txtTargetFile.Text);
          temp.SubItems.Add(txtOutputDir.Text);
          temp.ImageIndex = 2;
        }
      }
      else {
        // Save changes to listview 
        ListViewItem temp = listView1.Items[currentlyBeingEditedJob];
        temp.SubItems[0].Text = txtSourceFile.Text;
        temp.SubItems[1].Text = txtTargetFile.Text;
        temp.SubItems[2].Text = txtOutputDir.Text;
        btnAddEditJob.Text = "&Add to batch";
        btnResetForms.Text = "&Reset Forms";
      }
    }

    private void txtBatchSourceDir_DragEnter(object sender, DragEventArgs e) {
      if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (Directory.Exists(files[0])) e.Effect = DragDropEffects.All; // Check if the first one is an existing folder
      }
      else
        e.Effect = DragDropEffects.None;
    }

    private void txtBatchSourceDir_DragDrop(object sender, DragEventArgs e) {
      string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
      txtBatchSourceDir.Text = files[0]; // Take the first one
    }

    private void txtBatchTargetDir_DragEnter(object sender, DragEventArgs e) {
      if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (Directory.Exists(files[0])) e.Effect = DragDropEffects.All; // Check if the first one is an existing folder
      }
      else
        e.Effect = DragDropEffects.None;
    }

    private void txtBatchTargetDir_DragDrop(object sender, DragEventArgs e) {
      string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
      txtBatchTargetDir.Text = files[0]; // Take the first one
    }

    #endregion

    #region Batch Worker tab

    private void btnRemove_Click(object sender, EventArgs e) {
      RemoveTask();
    }

    private void RemoveTask() {
      foreach (ListViewItem temp in listView1.SelectedItems) {
        temp.Remove();
      }
    }

    private void btnClear_Click(object sender, EventArgs e) {
      this.listView1.Items.Clear(); // Goodbye~       
    }

    private void btnEdit_Click(object sender, EventArgs e) {
      EditTask();
    }

    private void EditTask() {
      if (listView1.SelectedItems.Count > 0) {
        ListViewItem temp = listView1.SelectedItems[0]; // Get the first pair
        currentlyBeingEditedJob = listView1.SelectedItems[0].Index;
        txtSourceFile.Text = temp.SubItems[0].Text;
        txtTargetFile.Text = temp.SubItems[1].Text;
        txtOutputDir.Text = temp.SubItems[2].Text;
        btnAddEditJob.Text = "&Save";
        btnResetForms.Text = "&Cancel";

        // Switch to File Patch tab
        tabControl1.SelectedTab = tabControl1.TabPages[0];
        txtSourceFile.Focus();
      }
    }

    private void editToolStripMenuItem_Click(object sender, EventArgs e) {
      EditTask();
    }

    private void removeToolStripMenuItem_Click(object sender, EventArgs e) {
      RemoveTask();
    }

    private void resetToolStripMenuItem_Click(object sender, EventArgs e) {
      foreach (ListViewItem temp in this.listView1.SelectedItems) {
        temp.ImageIndex = 2;
      }
    }

    private void btnBrowseBatchSourceDir_Click(object sender, EventArgs e) {
      if (fbdDirBrowser.ShowDialog() == DialogResult.OK) txtBatchSourceDir.Text = fbdDirBrowser.SelectedPath;
    }

    private void btnBrowseBatchTargetDir_Click(object sender, EventArgs e) {
      if (fbdDirBrowser.ShowDialog() == DialogResult.OK) txtBatchTargetDir.Text = fbdDirBrowser.SelectedPath;
    }

    private void btnBatchLoadDirs_Click(object sender, EventArgs e) {

      if(string.IsNullOrEmpty(txtBatchSourceDir.Text) || string.IsNullOrEmpty(txtBatchTargetDir.Text)) {
        MessageBox.Show("Please select source and target directories!");
        return;
      }

      //Basic implementation
      //Gets file list from source and target dirs, sorts them alphabetically
      //Limits the number of files to the amount of files in the directory with least files
      //Matches the first file in the source dir with the first file in the target dir, and so on


      var allSourceFiles = Directory.EnumerateFiles(txtBatchSourceDir.Text).OrderBy(s => s);
      var allTargetFiles = Directory.EnumerateFiles(txtBatchTargetDir.Text).OrderBy(s => s);

      int numFiles = allSourceFiles.Count() > allTargetFiles.Count() ? allTargetFiles.Count() : allSourceFiles.Count();

      if (numFiles == 0) {
        MessageBox.Show("No files found in one of the directories!");
        return;
      }

      var sourceFiles = allSourceFiles.Take(numFiles);
      var targetFiles = allTargetFiles.Take(numFiles);

      int i = 1;
      foreach (var entry in sourceFiles.Zip(targetFiles, (source, target) => (source, target))) {
        var lvi = listView1.Items.Add(entry.source);
        lvi.SubItems.Add(entry.target);
        lvi.SubItems.Add(txtOutputDir.Text + @"\" + i.ToString("00"));
        i++;
      }
    }

    #endregion

    #region Settings tab

    private void chbUseCusXdelPara_CheckedChanged(object sender, EventArgs e) {
      useCustomParamenter = chbUseCustomXdeltaParams.Checked;
    }

    private void txtCusXdelta_TextChanged(object sender, EventArgs e) {
      customParamenter = txtCustomXdeltaParams.Text;
    }

    private void chbDetEpNum_CheckedChanged(object sender, EventArgs e) {
      tryDetectingEpisodeNumber = chbDetEpNum.Checked;
    }

    private void btnSetxdeltaHighCompression_Click(object sender, EventArgs e) {
      txtCustomXdeltaParams.Text = "-e -f -7 -B1073741824 -S djw -s %source% %patched% %vcdiff%";
    }

    private void btnSetxdeltaHighMem_Click(object sender, EventArgs e) {
      txtCustomXdeltaParams.Text = "-e -f -B1073741824 -s %source% %patched% %vcdiff%";
    }

    private void btnSetxdeltaDefault_Click(object sender, EventArgs e) {
      txtCustomXdeltaParams.Text = "-e -f -s %source% %patched% %vcdiff%";
    }

    private void btnCusXdelHelp_Click(object sender, EventArgs e) {
      System.Diagnostics.Process.Start(@"http://xdelta.org/");
    }

    private void btnBrowseDefaultOutDir_Click(object sender, EventArgs e) {
      if (fbdDirBrowser.ShowDialog() == DialogResult.OK) {
        txtDefaultOutDir.Text = fbdDirBrowser.SelectedPath;
      }
    }

    private void rdbSourceDir_CheckedChanged(object sender, EventArgs e) {
      outputPlace = 0;
    }

    private void rdbTargetDir_CheckedChanged(object sender, EventArgs e) {
      outputPlace = 1;
    }

    private void rdbThisFol_CheckedChanged(object sender, EventArgs e) {
      outputPlace = 2;
    }

    private void chbRun64bitxdelta3_CheckedChanged(object sender, EventArgs e) {
      run64bitxdelta = chbRun64bitxdelta3.Checked;
    }

    private void chbDist64bitxdelta3_CheckedChanged(object sender, EventArgs e) {
      dist64bitxdelta = chbDist64bitxdelta3.Checked;
    }

    private void btnApplySetxdeltaDefault_Click(object sender, EventArgs e) {
      txtCustomXdeltaParamsForApplying.Text = "-d -f -s %source% %vcdiff% %output%";
    }

    private void chbFunnyMode_CheckedChanged(object sender, EventArgs e) {
      funnyMode = chbFunnyMode.Checked;
    }

    private void chbAddNewPatchToApplyAllScripts_CheckedChanged(object sender, EventArgs e) {
      addNewPatchToApplyAllScripts = chbAddNewPatchToApplyAllScripts.Checked;
    }

    private void chbAlwaysCopySourceFiles_CheckedChanged(object sender, EventArgs e) {
      alwaysCopySourceFiles = chbAlwaysCopySourceFiles.Checked;
    }

    private void chbSkipAlternativeScripts_CheckedChanged(object sender, EventArgs e) {
      skipAlternativeScripts = chbSkipAlternativeScripts.Checked;
    }

    #endregion

    #region Apply tab

    private void txtApplySource_DragEnter(object sender, DragEventArgs e) {
      if (e.Data.GetDataPresent(DataFormats.FileDrop)) // File only, you faggots!
        e.Effect = DragDropEffects.All;
      else
        e.Effect = DragDropEffects.None;
    }

    private void txtApplyVcdiffFile_DragEnter(object sender, DragEventArgs e) {
      if (e.Data.GetDataPresent(DataFormats.FileDrop)) // File only, you faggots!
        e.Effect = DragDropEffects.All;
      else
        e.Effect = DragDropEffects.None;
    }

    private void txtApplyOutput_DragEnter(object sender, DragEventArgs e) {
      if (e.Data.GetDataPresent(DataFormats.FileDrop)) // File only, you faggots!
        e.Effect = DragDropEffects.All;
      else
        e.Effect = DragDropEffects.None;
    }

    private void txtApplySource_DragDrop(object sender, DragEventArgs e) {
      string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
      txtApplySource.Text = files[0];
    }

    private void txtApplyVcdiffFile_DragDrop(object sender, DragEventArgs e) {
      string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
      txtApplyVcdiffFile.Text = files[0];
    }

    private void txtApplyOutput_DragDrop(object sender, DragEventArgs e) {
      string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
      txtApplyOutput.Text = files[0];
    }

    private void btnBrowseApplySource_Click(object sender, EventArgs e) {
      if (openFileDialog2.ShowDialog() == DialogResult.OK) txtApplySource.Text = openFileDialog2.FileName;
    }

    private void btnBrowseApplyVcdiffFile_Click(object sender, EventArgs e) {
      if (openFileDialog3.ShowDialog() == DialogResult.OK) txtApplyVcdiffFile.Text = openFileDialog3.FileName;
    }

    private void btnBrowseApplyOutput_Click(object sender, EventArgs e) {
      if (saveFileDialog1.InitialDirectory.Length < 1) saveFileDialog1.InitialDirectory = openFileDialog2.InitialDirectory;
      if (saveFileDialog1.ShowDialog() == DialogResult.OK) txtApplyOutput.Text = saveFileDialog1.FileName;
    }

    private void btnResetFormsApplyTab_Click(object sender, EventArgs e) {
      txtApplyOutput.Text = "";
      txtApplySource.Text = "";
      txtApplyVcdiffFile.Text = "";
    }

    #endregion

    #endregion

    #region Setting methods

    private bool loadSettingsSub_bool(string name, bool defaultValue) {
      string text = settings.Read(name);
      return (text != null) ? text == "true" : defaultValue;
    }

    private string loadSettingsSub_String(string name, string defaultValue) {
      string text = settings.Read(name);
      return (text != null) ? text : defaultValue;
    }

    private void loadSettings() {
      customParamenter = txtCustomXdeltaParams.Text = loadSettingsSub_String("Setting.CustomParamenter", txtCustomXdeltaParams.Text);
      useCustomParamenter = chbUseCustomXdeltaParams.Checked = loadSettingsSub_bool("Setting.UseCustomParamenter", chbUseCustomXdeltaParams.Checked);

      txtCustomXdeltaParamsForApplying.Text = loadSettingsSub_String("Setting.CustomApplyingParamenter", txtCustomXdeltaParamsForApplying.Text);
      chbUseCustomXdeltaParamsForApplying.Checked = loadSettingsSub_bool("Setting.UseCustomApplyingParamenter", chbUseCustomXdeltaParamsForApplying.Checked);

      tryDetectingEpisodeNumber = chbDetEpNum.Checked = loadSettingsSub_bool("Setting.DetectEpNum", chbDetEpNum.Checked);
      txtAddTextWhenSwap.Text = loadSettingsSub_String("Setting.AddThisTextWhenSwap", txtAddTextWhenSwap.Text);
      chbAddTextWhenSwap.Checked = loadSettingsSub_bool("Setting.AddTextWhenSwap", chbAddTextWhenSwap.Checked);
      chbNewAutoName.Checked = loadSettingsSub_bool("Setting.chbNewAutoName", chbNewAutoName.Checked);

      run64bitxdelta = chbRun64bitxdelta3.Checked = loadSettingsSub_bool("Setting.chbRun64bitxdelta3", chbRun64bitxdelta3.Checked);
      dist64bitxdelta = chbDist64bitxdelta3.Checked = loadSettingsSub_bool("Setting.chbDist64bitxdelta3", chbDist64bitxdelta3.Checked);

      string text = settings.Read("OutDir.Place2Go");
      int number = 0;
      if (int.TryParse(text, out number)) this.outputPlace = number;
      switch (outputPlace) {
        case 0: rdbSourceDir.Checked = true; break;
        case 1: rdbTargetDir.Checked = true; break;
        case 2: rdbThisFol.Checked = true; break;
        default: rdbSourceDir.Checked = true; break;
      }
      txtDefaultOutDir.Text = loadSettingsSub_String("OutDir.txtDefaultOutDir", txtDefaultOutDir.Text);

      funnyMode = chbFunnyMode.Checked = loadSettingsSub_bool("Setting.chbFunnyMode", chbFunnyMode.Checked);

      addNewPatchToApplyAllScripts = chbAddNewPatchToApplyAllScripts.Checked = loadSettingsSub_bool("Setting.chbAddNewPatchToApplyAllScripts", chbAddNewPatchToApplyAllScripts.Checked);
      alwaysCopySourceFiles = chbAlwaysCopySourceFiles.Checked = loadSettingsSub_bool("Setting.chbAlwaysCopySourceFiles", chbAlwaysCopySourceFiles.Checked);
      chbOnlyStoreFileNameInVCDIFF.Checked = loadSettingsSub_bool("Setting.chbOnlyStoreFileNameInVCDIFF", chbOnlyStoreFileNameInVCDIFF.Checked);
      skipAlternativeScripts = chbSkipAlternativeScripts.Checked = loadSettingsSub_bool("Setting.chbSkipAlternativeScripts", chbSkipAlternativeScripts.Checked);
      chbSaveFormsInCreatePatchTab.Checked = loadSettingsSub_bool("Setting.chbSaveFormsInCreatePatchTab", chbSaveFormsInCreatePatchTab.Checked);
      if (chbSaveFormsInCreatePatchTab.Checked) {
        txtSourceFile.Text = settings.Read("CreatePatch.txtSourceFile");
        txtTargetFile.Text = settings.Read("CreatePatch.txtTargetFile");
        txtOutputDir.Text = settings.Read("CreatePatch.txtOutputDir");
      }
    }

    private void saveSettings() {
      settings.Write("Setting.CustomParamenter", txtCustomXdeltaParams.Text);
      settings.Write("Setting.UseCustomParamenter", (useCustomParamenter) ? "true" : "false");
      settings.Write("Setting.CustomApplyingParamenter", txtCustomXdeltaParamsForApplying.Text);
      settings.Write("Setting.UseCustomApplyingParamenter", (chbUseCustomXdeltaParamsForApplying.Checked) ? "true" : "false");
      settings.Write("Setting.DetectEpNum", (tryDetectingEpisodeNumber) ? "true" : "false");
      settings.Write("Setting.AddThisTextWhenSwap", txtAddTextWhenSwap.Text);
      settings.Write("Setting.AddTextWhenSwap", (chbAddTextWhenSwap.Checked) ? "true" : "false");
      settings.Write("Setting.chbNewAutoName", (chbNewAutoName.Checked) ? "true" : "false");
      settings.Write("Setting.chbRun64bitxdelta3", (chbRun64bitxdelta3.Checked) ? "true" : "false");
      settings.Write("Setting.chbDist64bitxdelta3", (chbDist64bitxdelta3.Checked) ? "true" : "false");
      settings.Write("OutDir.Place2Go", this.outputPlace.ToString());
      settings.Write("OutDir.txtDefaultOutDir", this.txtDefaultOutDir.Text);
      settings.Write("Setting.chbOnlyStoreFileNameInVCDIFF", (chbOnlyStoreFileNameInVCDIFF.Checked) ? "true" : "false");
      settings.Write("Setting.chbFunnyMode", (chbFunnyMode.Checked) ? "true" : "false");
      settings.Write("Setting.chbAddNewPatchToApplyAllScripts", (chbAddNewPatchToApplyAllScripts.Checked) ? "true" : "false");
      settings.Write("Setting.chbAlwaysCopySourceFiles", (chbAlwaysCopySourceFiles.Checked) ? "true" : "false");
      settings.Write("Setting.chbSkipAlternativeScripts", (chbSkipAlternativeScripts.Checked) ? "true" : "false");
      settings.Write("Setting.chbSaveFormsInCreatePatchTab", (chbSaveFormsInCreatePatchTab.Checked) ? "true" : "false");
      if (chbSaveFormsInCreatePatchTab.Checked) {
        settings.Write("CreatePatch.txtSourceFile", txtSourceFile.Text);
        settings.Write("CreatePatch.txtTargetFile", txtTargetFile.Text);
        settings.Write("CreatePatch.txtOutputDir", txtOutputDir.Text);
      }
      else {
        settings.Write("CreatePatch.txtSourceFile", "");
        settings.Write("CreatePatch.txtTargetFile", "");
        settings.Write("CreatePatch.txtOutputDir", "");
      }
      settings.Close();
    }

    #endregion

  }
}
