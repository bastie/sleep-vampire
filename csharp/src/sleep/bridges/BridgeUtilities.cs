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
 
using sleep.runtime;
using sleep.interfaces;
using sleep.engine.types;

namespace sleep.bridges{
 
/**
 * A bridge is a class that bridges your applications API and sleep.  Bridges are created using interfaces from the sleep.interfaces package.  Arguments are passed to bridges generally in a java.util.Stack form.  The java.util.Stack of arguments contains sleep Scalar objects.  The BridgeUtilities makes it safer and easier for you to extract Java types from arguments.
 * 
 * <pre>
 * // some code to execute an internal add function, not a complete example
 * 
 * public class MyAddFunction implements Function
 * {
 *    public Scalar evaluate(String name, ScriptInstance script, java.util.Stack arguments) 
 *    {
 *       if (name.equals("&add"))
 *       {
 *          int a = BridgeUtilities.getInt(arguments, 0);  
 *          int b = BridgeUtilities.getInt(arguments, 0); 
 *  
 *          return SleepUtils.getScalar(a + b); 
 *       }
 * 
 *       return SleepUtils.getEmptyScalar();
 *    }
 * }
 * </pre>
 *
 */
public class BridgeUtilities
{
   /** converts the specified string to an array of bytes (useful as Sleep stores byte arrays to strings) */
   public static byte[] toByteArrayNoConversion(String textz)
   {
      byte[] data = new byte[textz.length()];

      for (int y = 0; y < data.Length; y++)
      {
         data[y] = (byte)textz.charAt(y);
      }

      return data;
   }

   /** grab an integer. if the stack is empty 0 will be returned. */
   public static int getInt(java.util.Stack<Object> arguments)
   {
      return getInt(arguments, 0);
   }

   /** grab an integer, if the stack is empty the default value will be returned */
   public static int getInt(java.util.Stack<Object> arguments, int defaultValue)
   {
      if (arguments.isEmpty())
         return defaultValue;

      return ((Scalar)arguments.pop()).intValue();
   }

   /** grab a class, if the stack is empty the default value will be returned */
   public static Type getClass(java.util.Stack<Object> arguments, Type defaultValue)
   {
      Object obj = getObject(arguments);
      if (obj == null) { return defaultValue; }
      return (Type)obj;
   }

   /** grab a long.  if the stack is empty 0 will be returned. */
   public static long getLong(java.util.Stack<Object> arguments)
   {
      return getLong(arguments, 0L);
   }

   /** grab a long, if the stack is empty the default value will be returned */
   public static long getLong(java.util.Stack<Object> arguments, long defaultValue)
   {
      if (arguments.isEmpty())
         return defaultValue;

      return ((Scalar)arguments.pop()).longValue();
   }

   /** grab a double.  if the stack is empty a 0 will be returned */
   public static double getDouble(java.util.Stack<Object> arguments)
   {
     return getDouble(arguments, 0.0); 
   }

   /** grab a double, if the stack is empty the default value will be returned */
   public static double getDouble(java.util.Stack<Object> arguments, double defaultValue)
   {
      if (arguments.isEmpty())
         return defaultValue;

      return ((Scalar)arguments.pop()).doubleValue();
   }

   /** extracts all named parameters from the argument stack.  this method returns a Map whose keys are strings
       and values are Scalars. */
   public static java.util.Map<Object,Object> extractNamedParameters(java.util.Stack<Object> args)
   {
      java.util.Map<Object,Object> rv = new java.util.HashMap<Object,Object>();
      java.util.Iterator<Object> i = args.iterator();
      while (i.hasNext())
      {
         Scalar temp = (Scalar)i.next();
         if (temp.objectValue() != null && temp.objectValue().getType() == typeof(KeyValuePair))
         {
            i.remove();
            KeyValuePair value = (KeyValuePair)temp.objectValue();
            rv.put(value.getKey().toString(), value.getValue());
         }
      }

      return rv;
   }

   /** grabs a scalar iterator, this can come from either an array or a closure called continuously until $null is returned. */
   public static java.util.Iterator<Object> getIterator(java.util.Stack<Object> arguments, ScriptInstance script)
   {
      if (arguments.isEmpty())
        return getArray(arguments).scalarIterator();

      Scalar temp = (Scalar)arguments.pop();
      return SleepUtils.getIterator(temp, script);
   }

   /** grab a sleep array, if the stack is empty a scalar array with no elements will be returned. */
   public static ScalarArray getArray(java.util.Stack<Object> arguments)
   {
      Scalar s = getScalar(arguments);
      if (s.getArray() == null)
         return SleepUtils.getArrayScalar().getArray();

      return s.getArray();
   }

   /** grab a sleep hash, if the stack is empty a scalar hash with no members will be returned. */
   public static ScalarHash getHash(java.util.Stack<Object> arguments)
   {
      if (arguments.isEmpty())
         return SleepUtils.getHashScalar().getHash();

      return ((Scalar)arguments.pop()).getHash();
   }


   /** grab a sleep array, if the grabbed array is a readonly array, a copy is returned.  if the stack is empty an array with no elements will be returned. */
   public static ScalarArray getWorkableArray(java.util.Stack<Object> arguments)
   {
      if (arguments.isEmpty())
         return SleepUtils.getArrayScalar().getArray();

      Scalar temp = (Scalar)arguments.pop();

      if (temp.getArray().getType() == typeof(sleep.runtime.CollectionWrapper))
      {
         ScalarArray array = SleepUtils.getArrayScalar().getArray();
         java.util.Iterator<Object> i = temp.getArray().scalarIterator();
         while(i.hasNext())
         {
            array.push((Scalar)i.next());
         } 

         return array;
      }

      return temp.getArray();
   }

   /** grab an object, if the stack is empty then null will be returned. */
   public static Object getObject(java.util.Stack<Object> arguments)
   {
      if (arguments.isEmpty())
         return null;

      return ((Scalar)arguments.pop()).objectValue();
   }

   /** retrieves an executable Function object from the stack.  Functions can be passed as closures
       or as a reference to a built-in Sleep subroutine i.e. &my_func. */
   public static SleepClosure getFunction(java.util.Stack<Object> arguments, ScriptInstance script)
   {
      Scalar temp = getScalar(arguments);
      SleepClosure func = SleepUtils.getFunctionFromScalar(temp, script);

      if (func == null)
      {
         throw new java.lang.IllegalArgumentException("expected &closure--received: " + SleepUtils.describe(temp));
      }

      return func;
   }

   /** grab a scalar, if the stack is empty the empty/null scalar will be returned. */
   public static Scalar getScalar(java.util.Stack<Object> arguments)
   {
      if (arguments.isEmpty())
         return SleepUtils.getEmptyScalar();

      return ((Scalar)arguments.pop());
   }

   /** grab a string, if the stack is empty or if the value is null the default value will be returned. */
   public static String getString(java.util.Stack<Object> arguments, String defaultValue)
   {
      if (arguments.isEmpty())
         return defaultValue;

      String temp = arguments.pop().toString();

      if (temp == null)
         return defaultValue;

      return temp;
   }

   private static readonly bool doReplace = java.io.File.separatorChar != '/';

   /** adjusts the file argument to accomodate for the current working directory */
   public static java.io.File toSleepFile(String text, ScriptInstance i)
   {
      if (text == null)
      {
         return i.cwd();
      }
      else if (doReplace)
      {
         text = text.replace('/', java.io.File.separatorChar); 
      }

      java.io.File f = new java.io.File(text);

      if (!f.isAbsolute() && text.length() > 0)
      {
         return new java.io.File(i.cwd(), text);
      }
      else
      {
         return f;
      }
   }

   /** returns a File object from a string argument, the path in the string argument is transformed such 
       that the character / will refer to the correct path separator for the current OS.  Returns null if
       no file is specified as an argument. */
   public static java.io.File getFile(java.util.Stack<Object> arguments, ScriptInstance i)
   {
      return toSleepFile(arguments.isEmpty() ? null : arguments.pop().toString(), i);
   }
 
   /** Pops a Key/Value pair object off of the argument stack.  A Key/Value pair is created using
       the => operator within Sleep scripts.  If the top argument on this stack was not created using
       =>, this function will try to parse a key/value pair using the pattern: [key]=[value] */
   public static KeyValuePair getKeyValuePair(java.util.Stack<Object> arguments)
   {
      Scalar temps = getScalar(arguments);

      if (temps.objectValue() != null && temps.objectValue().getType() == typeof(sleep.bridges.KeyValuePair))
         return (KeyValuePair)temps.objectValue();

      if (temps.getActualValue() != null)
      {
         Scalar key, value;
         String temp = temps.getActualValue().toString();

         if (temp.indexOf('=') > -1)
         {
            key   = SleepUtils.getScalar(temp.substring(0, temp.indexOf('=')));
            value = SleepUtils.getScalar(  temp.substring( temp.indexOf('=') + 1, temp.length() ) );
            return new KeyValuePair(key, value);
         }
      }

      throw new java.lang.IllegalArgumentException("attempted to pass a malformed key value pair: " + temps);
   }

   /** Flattens the specified scalar array.  The <var>toValue</var> field can be null. */
   public static Scalar flattenArray(Scalar fromValue, Scalar toValue)
   {
      return flattenIterator(fromValue.getArray().scalarIterator(), toValue);
   }

   /** Flattens the specified arrays within the specified iterator.  The <var>toValue</var> field can be null. */
   public static Scalar flattenIterator(java.util.Iterator<Object> i, Scalar toValue)
   {
      if (toValue == null) { toValue = SleepUtils.getArrayScalar(); }

      while (i.hasNext())
      {
         Scalar temp = (Scalar)i.next();

         if (temp.getArray() != null)
         {
            flattenArray(temp, toValue);
         }
         else
         {
            toValue.getArray().push(temp);
         }
      }

      return toValue;
   }

   /** initializes local scope based on argument stack */
   public static int initLocalScope(ScriptVariables vars, Variable localLevel, java.util.Stack<Object> locals)
   {
      int name = 1;

      Scalar args = SleepUtils.getArrayScalar();

      while (!locals.isEmpty())
      {
         Scalar lvar = (Scalar)locals.pop();

         if (lvar.getActualValue() != null && lvar.getActualValue().getType() == typeof(ObjectValue) && lvar.getActualValue().objectValue() != null && lvar.getActualValue().objectValue().getType() == typeof(KeyValuePair))
         {
            KeyValuePair kvp = (KeyValuePair)lvar.getActualValue().objectValue();

            if (!sleep.parser.Checkers.isVariable(kvp.getKey().toString()))
            {
               throw new java.lang.IllegalArgumentException("unreachable named parameter: " + kvp.getKey());
            }
            else
            {
               vars.setScalarLevel(kvp.getKey().toString(), kvp.getValue(), localLevel);
            }
         }
         else
         {
            args.getArray().push(lvar);
            vars.setScalarLevel("$"+name, lvar, localLevel);
            name++;
         }
      }

      vars.setScalarLevel("@_", args, localLevel);
      return name;
   }

   /** normalizes the index value based on the specified length */
   public static int normalize(int value, int length)
   {
      return value < 0 ? value + length : value;
   }

   /** returns true if value is an array or throws an appropriate exception if value is not an array.
    *  @param n the name of the &amp;function
    *  @param value the scalar to check
    */
   public static bool expectArray(String n, Scalar value)
   {
      if (value.getArray() == null)
      {
         throw new java.lang.IllegalArgumentException(n + ": expected array. received " + SleepUtils.describe(value));
      }

      return true;
   }
}
}