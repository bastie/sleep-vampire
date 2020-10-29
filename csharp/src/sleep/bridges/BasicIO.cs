/*
 * Copyright 2002-2020 Raphael Mudge
 * Copyright 2020 Sebastian Ritter
 *
 * Redistribution and use in source and binary forms, with or without modification, are
 * permitted provided that the following conditions are met:
 *
 * 1. Redistributions of source code must retain the above copyright notice, this list of
 *    conditions and the following disclaimer.
 *
 * 2. Redistributions in binary form must reproduce the above copyright notice, this list
 *    of conditions and the following disclaimer in the documentation and/or other materials
 *    provided with the distribution.
 *
 * 3. Neither the name of the copyright holder nor the names of its contributors may be
 *    used to endorse or promote products derived from this software without specific prior
 *    written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL
 * THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE
 * GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED
 * AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED
 * OF THE POSSIBILITY OF SUCH DAMAGE.
 */
using System;
using java = biz.ritter.javapi;

using  sleep.engine;
using  sleep.engine.types;
using  sleep.interfaces;
using  sleep.runtime;
using  sleep.bridges.io;
using  sleep.taint;

namespace sleep.bridges{
 
/** provides IO functions for the sleep language */
public class BasicIO : Loadable, Function
{
    public void scriptUnloaded(ScriptInstance aScript)
    {
    }

    public void scriptLoaded (ScriptInstance aScript)
    {
        java.util.Hashtable<Object,object> temp = aScript.getScriptEnvironment().getEnvironment();

        temp.put("__EXEC__", TaintUtils.Tainter(TaintUtils.Sensitive(this)));

        // predicates
        temp.put("-eof",     new iseof());

        // functions
        temp.put("&openf",      TaintUtils.Sensitive(new openf()));

        SocketFuncs f = new SocketFuncs();

        temp.put("&connect",    TaintUtils.Sensitive(f));
        temp.put("&listen",     f);
        temp.put("&exec",       TaintUtils.Sensitive(new exec()));
        temp.put("&fork",       new fork());
        temp.put("&allocate",   this);

        temp.put("&sleep",      new sleep());

        temp.put("&closef",     new closef());

        // ascii'sh read functions
        temp.put("&read",       new read());
        temp.put("&readln",     TaintUtils.Tainter(new readln()));
        temp.put("&readAll",    TaintUtils.Tainter(new readAll()));
        temp.put("&readc",      TaintUtils.Tainter(this));

        // binary i/o functions :)
        temp.put("&readb",      TaintUtils.Tainter(new readb()));
        temp.put("&consume",    new consume());
        temp.put("&writeb",     new writeb());

        temp.put("&bread",      TaintUtils.Tainter(new bread()));
        temp.put("&bwrite",     new bwrite());

        // object io functions
        temp.put("&readObject",      TaintUtils.Tainter(this));
        temp.put("&writeObject",     this);
        temp.put("&readAsObject",      TaintUtils.Tainter(this));
        temp.put("&writeAsObject",     this);
        temp.put("&sizeof", this);

        temp.put("&pack",       new pack());
        temp.put("&unpack",     new unpack());

        temp.put("&available",  new available());
        temp.put("&mark",       new mark());
        temp.put("&skip",       temp.get("&consume"));
        temp.put("&reset",      new reset());
        temp.put("&wait",       this);

        // typical ASCII'sh output functions
        temp.put("&print",      new print());

        temp.put("&setEncoding", this);

        println f_println = new println();
        temp.put("&println",    f_println);
        temp.put("&printf",    f_println); // I need to fix my unit tests to get rid of the printf function... grr
        temp.put("&printAll",   new printArray());
        temp.put("&printEOF",   new printEOF());

        temp.put("&getConsole", new getConsoleObject());

        /* integrity functions */
        temp.put("&checksum", this);
        temp.put("&digest",   this);
    }

    private static java.util.zip.Checksum getChecksum(String algorithm)
    {
       if (algorithm.equals("Adler32")) { return new java.util.zip.Adler32(); }
       if (algorithm.equals("CRC32")) { return new java.util.zip.CRC32(); }
       return null;
    }

    public Scalar evaluate(String n, ScriptInstance i, java.util.Stack<Object> l)
    {
       if (n.equals("&wait"))
       {
          IOObject a = (IOObject)BridgeUtilities.getObject(l);
          long    to = BridgeUtilities.getLong(l, 0);

          return a.wait(i.getScriptEnvironment(), to);
       }
       else if (n.equals("__EXEC__"))
       {
          Scalar rv = SleepUtils.getArrayScalar();

          try
          { 
             java.lang.Process proc  = java.lang.Runtime.getRuntime().exec(BridgeUtilities.getString(l, ""), null, i.cwd());

             IOObject reader = SleepUtils.getIOHandle(proc.getInputStream(), null);

             String text = null;
             while ((text = reader.readLine()) != null)
             {
                rv.getArray().push(SleepUtils.getScalar(text));
             }

             if (proc.waitFor() != 0)
             {
                i.getScriptEnvironment().flagError("abnormal termination: " + proc.exitValue());
             }
          }
          catch (java.lang.Exception ex)
          {
             i.getScriptEnvironment().flagError(ex);
          }

          return rv;
       }
       else if (n.equals("&writeObject") || n.equals("&writeAsObject"))
       {
          IOObject a = chooseSource(l, 2, i);
          while (!l.isEmpty())
          {
             Scalar   b = (Scalar)l.pop();
             try
             {
                java.io.ObjectOutputStream ois = new java.io.ObjectOutputStream(a.getWriter());

                if (n.equals("&writeAsObject"))
                {
                   ois.writeObject(b.objectValue());
                }
                else
                {
                   ois.writeObject(b);
                }
             }
             catch (Exception ex)
             {
                i.getScriptEnvironment().flagError(ex);
                a.close();
             }
          }
       }
       else if (n.equals("&readObject") || n.equals("&readAsObject"))
       {
          IOObject a = chooseSource(l, 1, i);
          try
          {
             java.io.ObjectInputStream ois = new java.io.ObjectInputStream(a.getReader());

             if (n.equals("&readAsObject"))
             {
                return SleepUtils.getScalar(ois.readObject());
             }
             else
             {
                Scalar value = (Scalar)ois.readObject();
                return value;
             }
          }
          catch (java.io.EOFException eofex)
          {
             a.close();
          }
          catch (java.lang.Exception ex)
          {
             i.getScriptEnvironment().flagError(ex);
             a.close();
          }
       }
       else if (n.equals("&allocate"))
       {
          int capacity = BridgeUtilities.getInt(l, 1024 * 32); // 32K initial buffer by default
          BufferObject temp = new BufferObject();
          temp.allocate(capacity);
          return SleepUtils.getScalar(temp);
       }
       else if (n.equals("&digest"))
       {
          Scalar   s = BridgeUtilities.getScalar(l);
          if (s.objectValue() != null && s.objectValue() is IOObject)
          {
             /* do our fun stuff to setup a checksum object */

             bool isRead  = true;

             String temp = BridgeUtilities.getString(l, "MD5");
             if (temp.charAt(0) == '>')
             {
                isRead  = false;
                temp    = temp.substring(1);
             }
             
             IOObject io = (IOObject)s.objectValue();

             try
             {
                if (isRead)             {
                   java.security.DigestInputStream cis = new java.security.DigestInputStream(io.getInputStream(), java.security.MessageDigest.getInstance(temp));
                   io.openRead(cis);
                   return SleepUtils.getScalar(cis.getMessageDigest());
                }
                else
                {
                   java.security.DigestOutputStream cos = new java.security.DigestOutputStream(io.getOutputStream(), java.security.MessageDigest.getInstance(temp));
                   io.openWrite(cos);
                   return SleepUtils.getScalar(cos.getMessageDigest());
                }
             }
             catch (java.security.NoSuchAlgorithmException ex)
             {
                i.getScriptEnvironment().flagError(ex);
             }
          }
          else if (s.objectValue() != null && s.objectValue() is java.security.MessageDigest)
          {
             java.security.MessageDigest sum = (java.security.MessageDigest)s.objectValue();
             return SleepUtils.getScalar(sum.digest());
          }
          else
          {
             String temp = s.toString();
             String algo = BridgeUtilities.getString(l, "MD5");
             try
             {

                java.security.MessageDigest doit = java.security.MessageDigest.getInstance(algo);
                doit.update(BridgeUtilities.toByteArrayNoConversion(temp), 0, temp.length());
                return SleepUtils.getScalar(doit.digest());
             }
             catch (java.security.NoSuchAlgorithmException ex)
             {
                i.getScriptEnvironment().flagError(ex);
             }
          }

          return SleepUtils.getEmptyScalar();
       }
       else if (n.equals("&sizeof"))
       {
          return SleepUtils.getScalar(DataPattern.EstimateSize(BridgeUtilities.getString(l, "")));
       }
       else if (n.equals("&setEncoding"))
       {
          IOObject a    = chooseSource(l, 1, i);
          String   name = BridgeUtilities.getString(l, "");
 
          try
          {
             a.setEncoding(name);
          }
          catch (Exception ex)
          {
             throw new java.lang.IllegalArgumentException("&setEncoding: specified a non-existent encoding '" + name + "'");
          }
       }
       else if (n.equals("&readc"))
       {
          IOObject a    = chooseSource(l, 1, i);
          return SleepUtils.getScalar(a.readCharacter());
       }
       else if (n.equals("&checksum"))
       {
          Scalar   s = BridgeUtilities.getScalar(l);
          if (s.objectValue() != null && s.objectValue() is IOObject)
          {
             /* do our fun stuff to setup a checksum object */

             bool isRead  = true;

             String temp = BridgeUtilities.getString(l, "CRC32");
             if (temp.charAt(0) == '>')
             {
                isRead  = false;
                temp    = temp.substring(1);
             }
             
             IOObject io = (IOObject)s.objectValue();

             if (isRead)
             {
                java.util.zip.CheckedInputStream cis = new java.util.zip.CheckedInputStream(io.getInputStream(), getChecksum(temp));
                io.openRead(cis);
                return SleepUtils.getScalar(cis.getChecksum());
             }
             else
             {
                java.util.zip.CheckedOutputStream cos = new java.util.zip.CheckedOutputStream(io.getOutputStream(), getChecksum(temp));
                io.openWrite(cos);
                return SleepUtils.getScalar(cos.getChecksum());
             }
          }
          else if (s.objectValue() != null && s.objectValue() is java.util.zip.Checksum)
          {
             java.util.zip.Checksum sum = (java.util.zip.Checksum)s.objectValue();
             return SleepUtils.getScalar(sum.getValue());
          }
          else
          {
             String temp = s.toString();
             String algo = BridgeUtilities.getString(l, "CRC32");

             java.util.zip.Checksum doit = getChecksum(algo);
             doit.update(BridgeUtilities.toByteArrayNoConversion(temp), 0, temp.length());
             return SleepUtils.getScalar(doit.getValue());
          }
       }

       return SleepUtils.getEmptyScalar();
    }

    private class openf : Function
    {
       public Scalar evaluate(String n, ScriptInstance i, java.util.Stack<Object> l)
       {
          String a = ((Scalar)l.pop()).toString();

          FileObject temp = new FileObject();
          temp.open(a, i.getScriptEnvironment());

          return SleepUtils.getScalar(temp);
       }
    }

    private class exec : Function
    {
       public Scalar evaluate(String n, ScriptInstance i, java.util.Stack<Object> l)
       {
          Scalar   cmd      = l.isEmpty() ? SleepUtils.getEmptyScalar() : (Scalar)l.pop();
          String[]   command;

          if (cmd.getArray() != null)
          {
             command = (String[])(SleepUtils.getListFromArray(cmd.getArray()).toArray(new String[0])); 
          }
          else
          {
             command = cmd.toString().split("\\s");
          }

          String[] envp      = null;
          java.io.File     start     = null;

          if (!l.isEmpty())
          {
             if (SleepUtils.isEmptyScalar((Scalar)l.peek()))
             {
                l.pop();
             }
             else
             {
                ScalarHash env  = BridgeUtilities.getHash(l);
                java.util.Iterator<Object>   keys = env.keys().scalarIterator();
                envp = new String[env.keys().size()];
                for (int x = 0; x < envp.Length; x++)
                {
                   Scalar key = (Scalar)keys.next();
                   envp[x] = key.toString() + "=" + env.getAt(key);
                }
             }
          }

          if (!l.isEmpty() && !SleepUtils.isEmptyScalar((Scalar)l.peek()))
          {
             if (SleepUtils.isEmptyScalar((Scalar)l.peek()))
             {
                l.pop();
             }
             else
             {
                start = BridgeUtilities.getFile(l, i); 
             }
          }

          ProcessObject temp = new ProcessObject();
          temp.open(command, envp, start, i.getScriptEnvironment());

          return SleepUtils.getScalar(temp);
       }
    }

    private class sleep : Function
    {
       public Scalar evaluate(String n, ScriptInstance i, java.util.Stack<Object> l)
       {
          try
          {
             java.lang.Thread.currentThread().sleep(BridgeUtilities.getLong(l, 0));
          }
          catch (Exception ex) { }

          return SleepUtils.getEmptyScalar();
       }
    }

    private class fork : Function
    {
       public Scalar evaluate(String n, ScriptInstance i, java.util.Stack<Object> l)
       {
          SleepClosure   param = BridgeUtilities.getFunction(l, i);        

          // create our fork...
          ScriptInstance child = i.fork();
          child.installBlock(param.getRunnableCode());

          ScriptVariables vars = child.getScriptVariables();

          while (!l.isEmpty())
          {
             KeyValuePair kvp = BridgeUtilities.getKeyValuePair(l);
             vars.putScalar(kvp.getKey().toString(), SleepUtils.getScalar(kvp.getValue()));
          }

          // create a pipe between these two items...
          IOObject parent_io = new IOObject();
          IOObject child_io  = new IOObject();

          try
          {
             java.io.PipedInputStream  parent_in  = new java.io.PipedInputStream();
             java.io.PipedOutputStream parent_out = new java.io.PipedOutputStream();
             parent_in.connect(parent_out);

             java.io.PipedInputStream  child_in   = new java.io.PipedInputStream();
             java.io.PipedOutputStream child_out  = new java.io.PipedOutputStream();
             child_in.connect(child_out);

             parent_io.openRead(child_in);
             parent_io.openWrite(parent_out);

             child_io.openRead(parent_in);
             child_io.openWrite(child_out);
          
             child.getScriptVariables().putScalar("$source", SleepUtils.getScalar(child_io));

             java.lang.Thread temp = new java.lang.Thread(child, "fork of " + child.getRunnableBlock().getSourceLocation());

             parent_io.setThread(temp);
             child_io.setThread(temp);

             child.setParent(parent_io);

             temp.start();
          }
          catch (java.lang.Exception ex)
          {
             i.getScriptEnvironment().flagError(ex);
          }

          return SleepUtils.getScalar(parent_io);
       }
    }

    private class SocketFuncs : Function
    {
       public Scalar evaluate(String n, ScriptInstance i, java.util.Stack<Object> l)
       {
          java.util.Map<Object,Object> options = BridgeUtilities.extractNamedParameters(l);

          SocketObject.SocketHandler handler = new SocketObject.SocketHandler();
          handler.socket        = new SocketObject();
          handler.script        = i;

          handler.lport    = options.containsKey("lport") ? ((Scalar)options.get("lport")).intValue() : 0; /* 0 means use any free port */
          handler.laddr    = options.containsKey("laddr") ? ((Scalar)options.get("laddr")).toString() : null;
          handler.linger   = options.containsKey("linger") ? ((Scalar)options.get("linger")).intValue() : 5; /* 5ms is the default linger */
          handler.backlog  = options.containsKey("backlog") ? ((Scalar)options.get("backlog")).intValue() : 0; /* backlog of 0 means use default */

          if (n.equals("&listen"))
          {
             handler.port     = BridgeUtilities.getInt(l, -1);          // port
             handler.timeout  = BridgeUtilities.getInt(l, 60 * 1000);   // timeout
             handler.callback = BridgeUtilities.getScalar(l);           // scalar to put info in to

             handler.type     = SocketObject.LISTEN_FUNCTION;
          }
          else
          {
             handler.host     = BridgeUtilities.getString(l, "127.0.0.1");
             handler.port     = BridgeUtilities.getInt(l, 1);
             handler.timeout  = BridgeUtilities.getInt(l, 60 * 1000);   // timeout

             handler.type     = SocketObject.CONNECT_FUNCTION;
          }
          
          if (!l.isEmpty())
             handler.function = BridgeUtilities.getFunction(l, i);

          handler.start();

          return SleepUtils.getScalar(handler.socket);
       }
    }

    private class closef : Function
    {
       public Scalar evaluate(String n, ScriptInstance i, java.util.Stack<Object> l)
       {
          if (!l.isEmpty() && ((Scalar)l.peek()).objectValue() is IOObject)
          {
             IOObject a = (IOObject)BridgeUtilities.getObject(l);
             a.close();
          }
          else
          {
             int port = BridgeUtilities.getInt(l, 80);
             SocketObject.release(port);
          }

          return SleepUtils.getEmptyScalar();
       }
    }

    private class readln : Function
    {
       public Scalar evaluate(String n, ScriptInstance i, java.util.Stack<Object> l)
       {
          IOObject a = chooseSource(l, 1, i);
    
          String temp = a.readLine();

          if (temp == null)
          {
             return SleepUtils.getEmptyScalar();
          }

          return SleepUtils.getScalar(temp);
       }
    }

    private class readAll : Function
    {
       public Scalar evaluate(String n, ScriptInstance i, java.util.Stack<Object> l)
       {
          IOObject a = chooseSource(l, 1, i);

          Scalar ar = SleepUtils.getArrayScalar();
          
          String temp;
          while ((temp = a.readLine()) != null)
          {
             ar.getArray().push(SleepUtils.getScalar(temp));
          }

          return ar;
       }
    }

    private class println : Function
    {
       public Scalar evaluate(String n, ScriptInstance i, java.util.Stack<Object> l)
       {
          IOObject a = chooseSource(l, 2, i);

          String temp = BridgeUtilities.getString(l, "");
          a.printLine(temp);

          return SleepUtils.getEmptyScalar();
       }
    }

    private class printArray : Function
    {
       public Scalar evaluate(String n, ScriptInstance inst, java.util.Stack<Object> l)
       {
          IOObject a       = chooseSource(l, 2, inst);

          java.util.Iterator<Object> i = BridgeUtilities.getIterator(l, inst);
          while (i.hasNext())
          {
             a.printLine(i.next().toString());
          }

          return SleepUtils.getEmptyScalar();
       }
    }

    private class print : Function
    {
       public Scalar evaluate(String n, ScriptInstance i, java.util.Stack<Object> l)
       {
          IOObject a = chooseSource(l, 2, i);

          String temp = BridgeUtilities.getString(l, "");
          a.print(temp);

          return SleepUtils.getEmptyScalar();
       }
    }


    private class printEOF : Function
    {
       public Scalar evaluate(String n, ScriptInstance i, java.util.Stack<Object> l)
       {
          IOObject a = chooseSource(l, 1, i);
          a.sendEOF();

          return SleepUtils.getEmptyScalar();
       }
    }

    private static IOObject chooseSource(java.util.Stack<Object> l, int args, ScriptInstance i)
    {
       if (l.size() < args && !l.isEmpty())
       {
          Scalar temp = (Scalar)l.peek();

          if (temp.getActualValue() != null && temp.getActualValue().getType() == typeof(ObjectValue) && temp.objectValue() is IOObject)
          {
             l.pop();
             return (IOObject)temp.objectValue();
          }
       }
       else if (l.size() >= args)
       {
          Scalar b = (Scalar)l.pop();

          if (!(b.objectValue() is IOObject))
          {
             throw new java.lang.IllegalArgumentException("expected I/O handle argument, received: " + SleepUtils.describe(b));
          }

          return (IOObject)b.objectValue();
       }

       return IOObject.getConsole(i.getScriptEnvironment());
    }

    private class getConsoleObject : Function
    {
       public Scalar evaluate(String n, ScriptInstance i, java.util.Stack<Object> l)
       {
          return SleepUtils.getScalar(IOObject.getConsole(i.getScriptEnvironment()));
       }
    }

    private static Scalar ReadFormatted(String format, java.io.InputStream inJ, ScriptEnvironment env, IOObject control)
    {
       Scalar temp         = SleepUtils.getArrayScalar();
       DataPattern pattern = DataPattern.Parse(format);

       byte[]        bdata = new byte[8]; 
       java.nio.ByteBuffer  buffer  = java.nio.ByteBuffer.wrap(bdata);
       int         read    = 0;
       int         early, later;

       while (pattern != null)
       {
          buffer.order(pattern.order);

          if (pattern.value == 'M')
          {
             if (pattern.count == 1)
                pattern.count = 1024 * 10; // 10K worth of data :)

             inJ.mark(pattern.count);
          }
          else if (pattern.value == 'x')
          {
             try
             {
                inJ.skip(pattern.count);
             }
             catch (java.lang.Exception ex) { }
          }
          else if (pattern.value == 'h' || pattern.value == 'H')
          {
             java.lang.StringBuffer temps = new java.lang.StringBuffer();

             try
             {
                for (int z = 0; (z < pattern.count || pattern.count == -1); z++)
                {
                   read = inJ.read(bdata, 0, 1);

                   if (read < 1) throw new java.io.EOFException();
 
                   early = (buffer.get(0) & 0x00F0) >> 4;
                   later = (buffer.get(0) & 0x000F);

                   if (pattern.value == 'h')
                   {
                      temps.append(java.lang.Integer.toHexString(later));
                      temps.append(java.lang.Integer.toHexString(early));
                   }
                   else
                   {
                      temps.append(java.lang.Integer.toHexString(early));
                      temps.append(java.lang.Integer.toHexString(later));
                   }
                }
             }
             catch (java.lang.Exception fex) 
             { 
                if (control != null) control.close();
                temp.getArray().push(SleepUtils.getScalar(temps.toString()));       
                return temp;
             }
 
             temp.getArray().push( SleepUtils.getScalar(temps.toString()) ); // reads in a full on string :)
          }
          else if (pattern.value == 'z' || pattern.value == 'Z' || pattern.value == 'U' || pattern.value == 'u')
          {
             java.lang.StringBuffer temps = new java.lang.StringBuffer();
             int tempval;

             try
             {
                if (pattern.value == 'u' || pattern.value == 'U')
                {
                   read = inJ.read(bdata, 0, 2);
                   if (read < 2) throw new java.io.EOFException();
                   tempval = (int)buffer.getChar(0);
                }
                else
                {
                   tempval = inJ.read();
                   if (tempval == -1) throw new java.io.EOFException();
                }
             
                int z = 1;

                for (; tempval != 0 && z != pattern.count; z++)
                {
                   temps.append((char)tempval);

                   if (pattern.value == 'u' || pattern.value == 'U')
                   {
                      read = inJ.read(bdata, 0, 2);
                      if (read < 2) throw new java.io.EOFException();
                      tempval = (int)buffer.getChar(0);
                   }
                   else
                   {
                      tempval = inJ.read();
                      if (tempval == -1) throw new java.io.EOFException();
                   }
                } 

                if (tempval != 0)
                {
                   temps.append((char)tempval); 
                }

                if ((pattern.value == 'Z' || pattern.value == 'U') && z < pattern.count)
                {
                   int skipby = (pattern.count - z) * (pattern.value == 'U' ? 2 : 1);
                   inJ.skip(skipby);
                }
             }
             catch (java.lang.Exception fex) 
             { 
                if (control != null) control.close();
                temp.getArray().push(SleepUtils.getScalar(temps.toString()));       
                return temp;
             }
 
             temp.getArray().push( SleepUtils.getScalar(temps.toString()) ); // reads in a full on string :)
          }
          else
          {
             for (int z = 0; z != pattern.count; z++) // pattern.count is the integer specified "AFTER" the letter
             {
                Scalar value = null;
 
                try
                {
                   switch (pattern.value)
                   {
                      case 'R':
                        inJ.reset();
                        break;
                      case 'C':
                        read = inJ.read(bdata, 0, 1);

                        if (read < 1) throw new java.io.EOFException();

                        value = SleepUtils.getScalar((char)bdata[0] + ""); // turns the char into a string
                        break;
                      case 'c':
                        read = inJ.read(bdata, 0, 2);

                        if (read < 2) throw new java.io.EOFException();

                        value = SleepUtils.getScalar(buffer.getChar(0) + ""); // turns the char into a string
                        break;
                      case 'b':
                        bdata[0] = (byte)inJ.read();

                        if (bdata[0] == -1) throw new java.io.EOFException();

                        value = SleepUtils.getScalar((int)bdata[0]); // turns the byte into an int
                        break;
                      case 'B':
                        read = inJ.read();

                        if (read == -1) throw new java.io.EOFException();

                        value = SleepUtils.getScalar(read);
                        break;
                      case 's':
                        read = inJ.read(bdata, 0, 2);

                        if (read < 2) throw new java.io.EOFException();

                        value = SleepUtils.getScalar(buffer.getShort(0));
                        break;
                      case 'S':
                        read = inJ.read(bdata, 0, 2);

                        if (read < 2) throw new java.io.EOFException();

                        value = SleepUtils.getScalar((int)buffer.getShort(0) & 0x0000FFFF);
                        break;
                      case 'i':
                        read = inJ.read(bdata, 0, 4);

                        if (read < 4) throw new java.io.EOFException();

                        value = SleepUtils.getScalar(buffer.getInt(0)); // turns the byte into an int
                        break;
                      case 'I':
                        read = inJ.read(bdata, 0, 4);

                        if (read < 4) throw new java.io.EOFException();

                        value = SleepUtils.getScalar((long)buffer.getInt(0) & 0x00000000FFFFFFFFL); // turns the byte into an int
                        break;
                      case 'f':
                        read = inJ.read(bdata, 0, 4);

                        if (read < 4) throw new java.io.EOFException();

                        value = SleepUtils.getScalar(buffer.getFloat(0)); // turns the byte into an int
                        break;
                      case 'd':
                        read = inJ.read(bdata, 0, 8);

                        if (read < 8) throw new java.io.EOFException();

                        value = SleepUtils.getScalar(buffer.getDouble(0)); // turns the byte into an int
                        break;
                      case 'l':
                        read = inJ.read(bdata, 0, 8);

                        if (read < 8) throw new java.io.EOFException();

                        value = SleepUtils.getScalar(buffer.getLong(0)); // turns the byte into an int
                        break;
                      case 'o':
                        java.io.ObjectInputStream ois = new java.io.ObjectInputStream(inJ);
                        value = (Scalar)ois.readObject();
                        break;

                      default:
                        env.showDebugMessage("unknown file pattern character: " + pattern.value);
                        break;
                   }
                }
                catch (java.lang.Exception ex) 
                { 
                   if (control != null) control.close();
                   if (value != null)   
                      temp.getArray().push(value);       
                   return temp;
                }
 
                if (value != null)   
                   temp.getArray().push(value);       
             }
          }

          pattern = pattern.next;
       }

       return temp;
    }

    private static void WriteFormatted(String format, java.io.OutputStream outJ, ScriptEnvironment env, java.util.Stack<Object> arguments, IOObject control)
    {
       DataPattern pattern  = DataPattern.Parse(format);

       if (arguments.size() == 1 && ((Scalar)arguments.peek()).getArray() != null)
       {
          java.util.Stack<Object> temp = new java.util.Stack<Object>();
          java.util.Iterator<Object> i = ((Scalar)arguments.peek()).getArray().scalarIterator();
          while (i.hasNext())
              temp.push(i.next());

          WriteFormatted(format, outJ, env, temp, control);
          return;
       }

       byte[]        bdata = new byte[8]; 
       java.nio.ByteBuffer  buffer  = java.nio.ByteBuffer.wrap(bdata);

       while (pattern != null)
       {
          buffer.order(pattern.order);

          if (pattern.value == 'z' || pattern.value == 'Z' || pattern.value == 'u' || pattern.value == 'U')
          {
             try
             {
                char[] tempchars = BridgeUtilities.getString(arguments, "").toCharArray();

                for (int y = 0; y < tempchars.Length; y++)
                {
                   if (pattern.value == 'u' || pattern.value == 'U')
                   {
                      buffer.putChar(0, tempchars[y]);
                      outJ.write(bdata, 0, 2);
                   }
                   else
                   {
                      outJ.write((int)tempchars[y]);
                   }
                }

                // handle padding... 

                for (int z = tempchars.Length; z < pattern.count; z++)
                {
                   switch (pattern.value)
                   {
                      case 'U':
                         outJ.write(0); 
                         outJ.write(0);
                         break;
                      case 'Z':
                         outJ.write(0);
                         break;
                   }
                }

                // write out our terminating null byte please...

                if (pattern.value == 'z' || (pattern.value == 'Z' && pattern.count == -1))
                {
                   outJ.write(0);
                }
                else if (pattern.value == 'u' || (pattern.value == 'U' && pattern.count == -1))
                {
                   outJ.write(0);
                   outJ.write(0);
                }
             }
             catch (java.lang.Exception ex)
             {
                if (control != null) control.close();
                return;
             }
          }
          else if (pattern.value == 'h' || pattern.value == 'H')
          {
             try
             {
                java.lang.StringBuffer number = new java.lang.StringBuffer("FF");
                String       argzz  = BridgeUtilities.getString(arguments, "");
             
                if ((argzz.length() % 2) != 0)
                {
                   throw new java.lang.IllegalArgumentException("can not pack '" + argzz + "' as hex string, number of characters must be even");
                }

                char[] tempchars = argzz.toCharArray();

                for (int y = 0; y < tempchars.Length; y += 2)
                {
                   if (pattern.value == 'H')
                   {
                      number.setCharAt(0, tempchars[y]);
                      number.setCharAt(1, tempchars[y+1]);
                   }
                   else
                   {
                      number.setCharAt(0, tempchars[y+1]);
                      number.setCharAt(1, tempchars[y]);
                   }

                   buffer.putInt(0, java.lang.Integer.parseInt(number.toString(), 16));
                   outJ.write(bdata, 3, 1);
                }
             }
             catch (java.lang.IllegalArgumentException aex)
             {
                if (control != null) control.close();
                throw (aex);
             }
             catch (java.lang.Exception ex)
             {
                ex.printStackTrace();
                if (control != null) control.close();
                return;
             }
          }
          else
          {
             for (int z = 0; z != pattern.count && !arguments.isEmpty(); z++)
             {
                Scalar temp = null;

                if (pattern.value != 'x')
                {
                   temp = BridgeUtilities.getScalar(arguments);
                }

                try
                {
                   switch (pattern.value)
                   {
                      case 'x':
                        outJ.write(0);
                        break;
                      case 'c':
                        buffer.putChar(0, temp.toString().charAt(0));
                        outJ.write(bdata, 0, 2);
                        break;
                      case 'C':
                        outJ.write((int)temp.toString().charAt(0));
                        break;
                      case 'b':
                      case 'B':
                        outJ.write(temp.intValue());
                        break;
                      case 's':
                      case 'S':
                        buffer.putShort(0, (short)temp.intValue());
                        outJ.write(bdata, 0, 2);
                        break;
                      case 'i':
                        buffer.putInt(0, temp.intValue());
                        outJ.write(bdata, 0, 4);
                        break;
                      case 'I':
                        buffer.putInt(0, (int)temp.longValue());
                        outJ.write(bdata, 0, 4);
                        break;
                      case 'f':
                        buffer.putFloat(0, (float)temp.doubleValue());
                        outJ.write(bdata, 0, 4);
                        break;
                      case 'd':
                        buffer.putDouble(0, temp.doubleValue());
                        outJ.write(bdata, 0, 8);
                        break;
                      case 'l':
                        buffer.putLong(0, temp.longValue());
                        outJ.write(bdata, 0, 8);
                        break;
                      case 'o':
                        try
                        {
                           java.io.ObjectOutputStream oos = new java.io.ObjectOutputStream(outJ);
                           oos.writeObject(temp);
                        }
                        catch (java.lang.Exception ex)
                        {
                           env.flagError(ex);
                           if (control != null) control.close();
                           return;
                        }
                        break;
                      default:
                      break;
                   }
                }
                catch (java.lang.Exception ex) 
                { 
                   if (control != null) control.close();
                   return;
                }
             }
          }

          pattern = pattern.next;
       }

       try
       {
          outJ.flush();
       }
       catch (java.lang.Exception ex) { }
    }

    private class bread : Function
    {
       public Scalar evaluate(String n, ScriptInstance i, java.util.Stack<Object> l)
       {
          IOObject        a = chooseSource(l, 2, i);
          String    pattern = BridgeUtilities.getString(l, "");

          return a.getReader() != null ? ReadFormatted(pattern, a.getReader(), i.getScriptEnvironment(), a) : SleepUtils.getEmptyScalar();
       }
    }

    private class bwrite : Function
    {
       public Scalar evaluate(String n, ScriptInstance i, java.util.Stack<Object> l)
       {
          IOObject        a = chooseSource(l, 3, i);
          String    pattern = BridgeUtilities.getString(l, "");

          WriteFormatted(pattern, a.getWriter(), i.getScriptEnvironment(), l, a);
          return SleepUtils.getEmptyScalar();
       }
    }

    private class mark : Function
    {
       public Scalar evaluate(String n, ScriptInstance i, java.util.Stack<Object> l)
       {
          IOObject        a = chooseSource(l, 2, i);

          if (a.getInputBuffer() == null)
          {
             throw new java.lang.RuntimeException("&mark: input buffer for " + SleepUtils.describe(SleepUtils.getScalar(a)) + " is closed");
          }

          a.getInputBuffer().mark(BridgeUtilities.getInt(l, 1024 * 10 * 10));

          return SleepUtils.getEmptyScalar();
       }
    }

    private class available : Function
    {
       public Scalar evaluate(String n, ScriptInstance i, java.util.Stack<Object> l)
       {
          try
          {
             IOObject        a = chooseSource(l, 1, i);

             if (l.isEmpty())
             {
                return SleepUtils.getScalar(a.getInputBuffer().available());
             }
             else
             {
                String delim = BridgeUtilities.getString(l, "\n");

                java.lang.StringBuffer temp = new java.lang.StringBuffer();

                int x = 0;
                int y = a.getInputBuffer().available();

                a.getInputBuffer().mark(y);
                
                while (x < y)
                {
                   temp.append((char)a.getReader().readUnsignedByte());
                   x++;
                }

                a.getInputBuffer().reset();
      
                return SleepUtils.getScalar(temp.indexOf(delim) > -1);
             }
          }
          catch (java.lang.Exception ex) { return SleepUtils.getEmptyScalar(); }
       }
    }

    private class reset : Function
    {
       public Scalar evaluate(String n, ScriptInstance i, java.util.Stack<Object> l)
       {
          try {
          IOObject        a = chooseSource(l, 1, i);
          a.getInputBuffer().reset();
          } catch (java.lang.Exception ex) { }

          return SleepUtils.getEmptyScalar();
       }
    }

    private class unpack : Function
    {
       public Scalar evaluate(String n, ScriptInstance i, java.util.Stack<Object> l)
       {
          String    pattern = BridgeUtilities.getString(l, "");
          String    data    = BridgeUtilities.getString(l, "");

          try
          {
             java.io.ByteArrayOutputStream outJ = new java.io.ByteArrayOutputStream(data.length());
             java.io.DataOutputStream toBytes  = new java.io.DataOutputStream(outJ);
             toBytes.writeBytes(data);     

             return ReadFormatted(pattern, new java.io.DataInputStream(new java.io.ByteArrayInputStream(outJ.toByteArray())), i.getScriptEnvironment(), null);
          }
          catch (Exception ex)
          {
             return SleepUtils.getArrayScalar();
          }
       }
    }

    private class pack : Function
    {
       public Scalar evaluate(String n, ScriptInstance i, java.util.Stack<Object> l)
       {
          String    pattern = BridgeUtilities.getString(l, "");

          java.io.ByteArrayOutputStream temp = new java.io.ByteArrayOutputStream(DataPattern.EstimateSize(pattern) + 128);
         
          WriteFormatted(pattern, new java.io.DataOutputStream(temp), i.getScriptEnvironment(), l, null);

          return SleepUtils.getScalar(temp.toByteArray(), temp.size());
       }
    }

    private class writeb : Function
    {
       public Scalar evaluate(String n, ScriptInstance i, java.util.Stack<Object> l)
       {
          IOObject     a = chooseSource(l, 2, i);
          String    data = BridgeUtilities.getString(l, "");

          try
          {
             for (int x = 0; x < data.length(); x++)
             {
                a.getWriter().writeByte((byte)data.charAt(x));
             } 
             a.getWriter().flush();
          }
          catch (Exception ex)
          {
             a.close();
             i.getScriptEnvironment().flagError(ex);
          }

          return SleepUtils.getEmptyScalar();
       }
    }

    private class readb : Function
    {
       public Scalar evaluate(String n, ScriptInstance i, java.util.Stack<Object> l)
       {
          IOObject           a = chooseSource(l, 2, i);
          int               to = BridgeUtilities.getInt(l, 1);
          int             last = 0;
          byte[]          temp = null;
          java.lang.StringBuffer  buffer = null;

          if (a.getReader() != null)
          {
             int read = 0;

             try
             {
                if (to == -1)
                {
                   buffer = new java.lang.StringBuffer(BridgeUtilities.getInt(l, 2048));

                   while (true)
                   { 
                      last = a.getReader().read();

                      if (last == -1)
                         break;

                      char append = (char)(last & 0xFF);
                      buffer.append(append);      
       
                      read++; 
                   }
                }
                else
                {
                   temp = new byte[to];

                   while (read < to)
                   {
                      last = a.getReader().read(temp, read, to - read);

                      if (last == -1) { break; }
                      read += last;
                   } 
                }
             }
             catch (Exception ex)
             {
                a.close();

                if (to != -1)
                   i.getScriptEnvironment().flagError(ex);
             }

             if (read > 0)
             {
                if (temp != null)
                   return SleepUtils.getScalar(temp, read);

                if (buffer != null)
                   return SleepUtils.getScalar(buffer.toString());
             }
          }
          return SleepUtils.getEmptyScalar();
       }
    }

    private class consume : Function
    {
       public Scalar evaluate(String n, ScriptInstance i, java.util.Stack<Object> l)
       {
          IOObject         a = chooseSource(l, 2, i);
          int             to = BridgeUtilities.getInt(l, 1);
          int           size = BridgeUtilities.getInt(l, 1024 * 32); /* 32K buffer anyone */
          int           last = 0;

          if (a.getReader() != null)
          {
             byte[] temp = new byte[size];
  
             int read = 0;
 
             try
             {
                while (read < to)
                {
                   if ((to - read) < size)
                   {
                      last = a.getReader().read(temp, 0, to - read);
                   }
                   else
                   {
                      last = a.getReader().read(temp, 0, size);
                   }

                   if (last == -1) { break; }

                   read += last;
                }
             }
             catch (Exception ex)
             {
                a.close();
                i.getScriptEnvironment().flagError(ex);
             }

             if (read > 0)
             {
                return SleepUtils.getScalar(read);
             }
          }
          return SleepUtils.getEmptyScalar();
       }
    }

    private class read : Function
    {
       public Scalar evaluate(String n, ScriptInstance i, java.util.Stack<Object> l)
       {
          IOObject     a = chooseSource(l, 2, i);
          SleepClosure b = BridgeUtilities.getFunction(l, i);

          java.lang.Thread fred = new java.lang.Thread(new CallbackReader(a, i, b, BridgeUtilities.getInt(l, 0)));
          a.setThread(fred);
          fred.start();

          return SleepUtils.getEmptyScalar();
       }
    }

    private class iseof : Predicate
    {
       public bool decide(String n, ScriptInstance i, java.util.Stack<Object> l)
       {
          IOObject a = (IOObject)BridgeUtilities.getObject(l);
          return a.isEOF();
       }
    }

    private class CallbackReader : java.lang.Runnable
    {
       protected IOObject       source;
       protected ScriptInstance script;
       protected SleepClosure   function;
       protected int            bytes;
 
       public CallbackReader(IOObject s, ScriptInstance si, SleepClosure func, int byteme)
       {
          source   = s;
          script   = si;
          function = func;
          bytes    = byteme;
       }

       public void run()
       {
          java.util.Stack<Object>  args = new java.util.Stack<Object>();
          String temp;

          if (bytes <= 0)
          {
             while (script.isLoaded() && (temp = source.readLine()) != null)
             {
                args.push(SleepUtils.getScalar(temp));
                args.push(SleepUtils.getScalar(source));

                function.callClosure("&read", script, args);
             } 
          }
          else
          {
             java.lang.StringBuffer tempb = null;

             try
             {
                while (script.isLoaded() && !source.isEOF())
                {
                   tempb = new java.lang.StringBuffer(bytes);

                   for (int x = 0; x < bytes; x++)
                   {
                      tempb.append((char)source.getReader().readUnsignedByte());
                   }

                   args.push(SleepUtils.getScalar(tempb.toString()));
                   args.push(SleepUtils.getScalar(source));
  
                   function.callClosure("&read", script, args);
                }
             }
             catch (Exception ex)
             {
                if (tempb.length() > 0)
                {
                   args.push(SleepUtils.getScalar(tempb.toString()));
                   args.push(SleepUtils.getScalar(source));
  
                   function.callClosure("&read", script, args);
                }

                source.close();
                script.getScriptEnvironment().flagError(ex);
             }
          }
       }
    }
}
}