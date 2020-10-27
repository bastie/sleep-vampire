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

using  sleep.runtime;
using  sleep.engine.types;
using  sleep.interfaces;
using  sleep.bridges;

namespace sleep.engine{

/** This class is sort of the center of the HOES universe containing several methods for mapping 
    between Sleep and Java and resolving which mappings make sense. */
public class ObjectUtilities
{
   private static Type STRING_SCALAR;
   private static Type INT_SCALAR; 
   private static Type DOUBLE_SCALAR;
   private static Type LONG_SCALAR;
   private static Type OBJECT_SCALAR;

   static ObjectUtilities()
   {
      STRING_SCALAR = typeof(sleep.engine.types.StringValue);
      INT_SCALAR    = typeof(sleep.engine.types.IntValue);
      DOUBLE_SCALAR = typeof(sleep.engine.types.DoubleValue);
      LONG_SCALAR   = typeof(sleep.engine.types.LongValue);
      OBJECT_SCALAR = typeof(sleep.engine.types.ObjectValue);
   }

   /** when looking for a Java method that matches the sleep args, we use a Yes match immediately */
   public static readonly int ARG_MATCH_YES   = 3;
  
   /** when looking for a Java method that matches the sleep args, we immediately drop all of the no answers. */
   public static readonly int ARG_MATCH_NO    = 0;

   /** when looking for a Java method that matches the sleep args, we save the maybes and use them as a last resort if no yes match is found */
   public static readonly int ARG_MATCH_MAYBE = 1;

   /** convienence method to determine wether or not the stack of values is a safe match for the specified method signature */
   public static int isArgMatch(Type[] check, java.util.Stack<Object> arguments)
   {
      int value = ARG_MATCH_YES;

      for (int z = 0; z < check.length; z++)
      {
         Scalar scalar = (Scalar)arguments.get(check.length - z - 1);

         value = value & isArgMatch(check[z], scalar);

//         System.out.println("Matching: " + scalar + "(" + scalar.getValue().getClass() + "): to " + check[z] + ": " + value);
 
         if (value == ARG_MATCH_NO)
         {
            return ARG_MATCH_NO;
         }
      }

      return value;
   }

   /** converts the primitive version of the specified class to a regular usable version */
   private static Type normalizePrimitive(Type check)
   {
      if (check == Integer.TYPE) { check = typeof(Integer); }
      else if (check == Double.TYPE)   { check = typeof(Double); }
      else if (check == Long.TYPE)     { check = typeof(Long); }
      else if (check == Float.TYPE)    { check = typeof(Float); }
      else if (check == Boolean.TYPE)  { check = typeof(Boolean); }
      else if (check == Byte.TYPE)     { check = typeof(Byte); }
      else if (check == Character.TYPE) { check = typeof(Character); }
      else if (check == Short.TYPE)    { check = typeof(Short); }

      return check;
   }

   /** determined if the specified scalar can be rightfully cast to the specified class */
   public static int isArgMatch(Type check, Scalar scalar)
   {
      if (SleepUtils.isEmptyScalar(scalar))
      {
         return ARG_MATCH_YES;
      }
      else if (scalar.getArray() != null)
      {
         if (check.isArray())
         {
            Class compType = check.getComponentType(); /* find the actual nuts and bolts component type so we can work with it */
            while (compType.isArray())
            {
               compType = compType.getComponentType();
            }

            Class mytype = getArrayType(scalar, null);
 
            if (mytype != null && compType.isAssignableFrom(mytype))
            {
               return ARG_MATCH_YES;
            }
            else
            {
               return ARG_MATCH_NO;
            }
         }
         else if (check == typeof(java.util.List) || check == typeof(java.util.Collection))
         {
            // would a java.util.List or java.util.Collection satisfy the argument?
            return ARG_MATCH_YES;
         }
         else if (check == typeof(ScalarArray))
         {
            return ARG_MATCH_YES;
         }
         else if (check == typeof(java.lang.Object) || check == typeof (System.Object))
         {
            return ARG_MATCH_MAYBE;
         }
         else
         {
            return ARG_MATCH_NO;
         }
      }
      else if (scalar.getHash() != null)
      {
         if (check == typeof(java.util.Map))
         {
            // would a java.util.Map or java.util.Collection satisfy the argument?
            return ARG_MATCH_YES;
         }
         else if (check == typeof(ScalarHash))
         {
            return ARG_MATCH_YES;
         }
         else if (check == typeof(java.lang.Object) || check == typeof(System.Object))
         {
            return ARG_MATCH_MAYBE;
         }
         else
         {
            return ARG_MATCH_NO;
         }
      }
      else if (check.isPrimitive())
      {
         Class stemp = scalar.getActualValue().getType(); /* at this point we know scalar is not null, not a hash, and not an array */

         if (stemp == INT_SCALAR && check == Integer.TYPE)
         {
            return ARG_MATCH_YES;
         }
         else if (stemp == DOUBLE_SCALAR && check == Double.TYPE)
         {
            return ARG_MATCH_YES;
         }
         else if (stemp == LONG_SCALAR && check == Long.TYPE)
         {
            return ARG_MATCH_YES;
         }
         else if (check == Character.TYPE && stemp == STRING_SCALAR && scalar.getActualValue().toString().length() == 1)
         {
            return ARG_MATCH_YES;
         }
         else if (stemp == OBJECT_SCALAR)
         {
            check = normalizePrimitive(check);
            return (scalar.objectValue().getClass() == check) ? ARG_MATCH_YES : ARG_MATCH_NO;
         }
         else
         {
            /* this is my lazy way of saying allow Long, Int, and Double scalar types to be considered
               maybes... */
            return (stemp == STRING_SCALAR) ? ARG_MATCH_NO : ARG_MATCH_MAYBE;
         }
      }
      else if (check.isInterface())
      {
         if (SleepUtils.isFunctionScalar(scalar) || check.isInstance(scalar.objectValue()))
         {
            return ARG_MATCH_YES;
         }
         else
         {
            return ARG_MATCH_NO;
         }
      }
      else if (check == typeof(java.lang.StringJ) || check == typeof (System.String))
      {
         Class stemp = scalar.getActualValue().getType();
         return (stemp == STRING_SCALAR) ? ARG_MATCH_YES : ARG_MATCH_MAYBE;
      }
      else if (check == typeof (java.lang.Object) || check == typeof(System.Object))
      {
         return ARG_MATCH_MAYBE; /* we're vying for anything and this will match anything */
      }
      else if (check.isInstance(scalar.objectValue()))
      {
         Class stemp = scalar.getActualValue().getType();
         return (stemp == OBJECT_SCALAR) ? ARG_MATCH_YES : ARG_MATCH_MAYBE;
      }
      else if (check.isArray())
      {
         Class stemp = scalar.getActualValue().getType();
         if (stemp == STRING_SCALAR && (check.getComponentType() == Character.TYPE || check.getComponentType() == Byte.TYPE))
         {
            return ARG_MATCH_MAYBE;
         }
         else
         {
            return ARG_MATCH_NO;
         }
      }
      else
      {
         return ARG_MATCH_NO;
      }
   }

   /** attempts to find the method that is the closest match to the specified arguments */
   public static java.lang.reflect.Method findMethod(Type theClass, String method, java.util.Stack<Object> arguments)
   {
      int      size    = arguments.size();

      java.lang.reflect.Method   temp    = null;
      java.lang.reflect.Method[] methods = theClass.getMethods();

      for (int x = 0; x < methods.length; x++) 
      {
         if (methods[x].getName().equals(method) && methods[x].getParameterTypes().length == size)
         {
             if (size == 0)
                   return methods[x];

             int value = isArgMatch(methods[x].getParameterTypes(), arguments);
             if (value == ARG_MATCH_YES) 
                   return methods[x];

             if (value == ARG_MATCH_MAYBE)
                   temp = methods[x];
         }
      }

      return temp;
   }

   /** attempts to find the constructor that is the closest match to the arguments */
   public static java.lang.reflect.Constructor findConstructor(Type theClass, java.util.Stack<Object> arguments)
   {
      int      size    = arguments.size();

      Constructor   temp         = null;
      Constructor[] constructors = theClass.getConstructors();

      for (int x = 0; x < constructors.length; x++) 
      {
         if (constructors[x].getParameterTypes().length == size)
         {
             if (size == 0)
                   return constructors[x];

             int value = isArgMatch(constructors[x].getParameterTypes(), arguments);
             if (value == ARG_MATCH_YES)
                   return constructors[x];

             if (value == ARG_MATCH_MAYBE)
                   temp = constructors[x];
         }
      }

      return temp;
   }

   /** this function checks if the specified scalar is a Class literal and uses that if it is, otherwise description is converted to a string and the convertDescriptionToClass method is used */
   public static Type convertScalarDescriptionToClass(Scalar description)
   {
       if (description.objectValue() is Type)
       {
          return (Type)description.objectValue();
       }

       return convertDescriptionToClass(description.toString());
   }

   /** converts the one character class description to the specified Class type, i.e. z = boolean, c = char, b = byte, i = integer, etc.. */
   public static Type convertDescriptionToClass(String description)
   {
      if (description.length() != 1)
      {
         return null;
      }

      Type atype = null;

      switch (description.charAt(0))
      {
         case 'z':
            atype = Boolean.TYPE;
            break;
         case 'c':
            atype = Character.TYPE;
            break;
         case 'b':
            atype = Byte.TYPE;
            break;
         case 'h':
            atype = Short.TYPE;
            break;
         case 'i':
            atype = Integer.TYPE;
            break;
         case 'l':
            atype = Long.TYPE;
            break;
         case 'f':
            atype = Float.TYPE;
            break;
         case 'd':
            atype = Double.TYPE;
            break;
         case 'o':
            atype = typeof(System.Object);
            break;
         case '*':
            atype = null; 
            break;
      }

      return atype;
   }

   /** marshalls the Sleep value into a Java value of the specified type. */
   public static Object buildArgument(Type type, Scalar value, ScriptInstance script)
   {
      if (type == typeof(StringJ) || type == typeof (System.String))
      {
         return SleepUtils.isEmptyScalar(value) ? null : value.toString();
      }
      else if (value.getArray() != null)
      {
         if (type.isArray())
         {
            Class atype = getArrayType(value, type.getComponentType());

            Object arrayV = Array.newInstance(atype, value.getArray().size());
            Iterator i = value.getArray().scalarIterator();
            int x = 0;
            while (i.hasNext())
            {
                Scalar temp = (Scalar)i.next();
                Object blah = buildArgument(atype, temp, script);

                if ((blah == null && !atype.isPrimitive()) || atype.isInstance(blah) || atype.isPrimitive())
                {
                   Array.set(arrayV, x, blah);
                }
                else
                {
                   if (atype.isArray())
                   {
                      throw new RuntimeException("incorrect dimensions for conversion to " + type);
                   }
                   else
                   {
                      throw new RuntimeException(SleepUtils.describe(temp) + " at "+x+" is not compatible with " + atype.getName());
                   }
                }
                x++;
            }

            return arrayV;
         }
         else if (type == typeof(ScalarArray))
         {
            return value.objectValue();
         }
         else
         {
            return SleepUtils.getListFromArray(value);
         }
      }
      else if (value.getHash() != null)
      {
         if (type == typeof(ScalarHash))
         {
            return value.objectValue();
         }
         else
         {
            return SleepUtils.getMapFromHash(value);
         }
      }
      else if (type.isPrimitive())
      {
         if (type == Boolean.TYPE)
         {
            return Boolean.valueOf(value.intValue() != 0);
         }
         else if (type == Byte.TYPE)
         {
            return new Byte((byte)value.intValue());
         }
         else if (type == Character.TYPE)
         {
            return new Character(value.toString().charAt(0));
         }
         else if (type == Double.TYPE)
         {
            return new Double(value.doubleValue());
         }
         else if (type == Float.TYPE)
         {
            return new Float((float)value.doubleValue());
         }
         else if (type == Integer.TYPE)
         {
            return new Integer(value.intValue());
         }
         else if (type == Short.TYPE)
         {
            return new Short((short)value.intValue());
         }
         else if (type == Long.TYPE)
         {
            return new Long(value.longValue());
         }
      }
      else if (SleepUtils.isEmptyScalar(value))
      {
         return null;
      }
      else if (type.isArray() && value.getActualValue().getType() == typeof(sleep.engine.types.StringValue))
      {
         if (type.getComponentType() == Byte.TYPE || type.getComponentType() == typeof(Byte))
         {
            return BridgeUtilities.toByteArrayNoConversion(value.toString());
         }
         else if (type.getComponentType() == Character.TYPE || type.getComponentType() == typeof(Character))
         {
            return value.toString().toCharArray();
         }
      }
      else if (type.isInterface() && SleepUtils.isFunctionScalar(value))
      {
         return ProxyInterface.BuildInterface(type, SleepUtils.getFunctionFromScalar(value, script), script);
      }

      return value.objectValue();
   }

   /** utility to create a string representation of an incompatible argument choice */
   public static String buildArgumentErrorMessage(Type theClass, String method, Type[] expected, Object[] parameters)
   {
      StringBuffer tempa = new StringBuffer(method + "(");
      
      for (int x = 0; x < expected.length; x++)
      {
         tempa.append(expected[x].getName());

         if ((x + 1) < expected.length)
            tempa.append(", ");
      }
      tempa.append(")");

      StringBuffer tempb = new StringBuffer("(");
      for (int x = 0; x < parameters.length; x++)
      {
         if (parameters[x] != null)
            tempb.append(parameters[x].getClass().getName());
         else
            tempb.append("null");

         if ((x + 1) < parameters.length)
            tempb.append(", ");
      }
      tempb.append(")");

      return "bad arguments " + tempb.toString() + " for " + tempa.toString() + " in " + theClass;
   } 

   /** populates a Java array with Sleep values marshalled into values of the specified types. */
   public static Object[] buildArgumentArray(Type[] types, java.util.Stack<Object> arguments, ScriptInstance script)
   {
      Object[] parameters = new Object[types.length];

      for (int x = 0; x < parameters.length; x++)
      {
         Scalar temp = (Scalar)arguments.pop();
         parameters[x] = buildArgument(types[x], temp, script);
      }
 
      return parameters;
   }

   /** marshalls a Java type into the appropriate Sleep scalar.  The primitives value will force this method to also check
       if the Java type could map to an int, long, double, etc.  Use true when in doubt. */
   public static Scalar BuildScalar(bool primitives, Object value)
   {
      if (value == null)
         return SleepUtils.getEmptyScalar();

      Type check = value.getClass();

      if (check.isArray())
      {
         if (check.getComponentType() == Byte.TYPE || check.getComponentType() == typeof(Byte))
         {
            return SleepUtils.getScalar((byte[])value);            
         }
         else if (check.getComponentType() == Character.TYPE || check.getComponentType() == typeof(Character))
         {
            return SleepUtils.getScalar(new String((char[])value));            
         }
         else
         {
            Scalar array = SleepUtils.getArrayScalar();
            for (int x = 0; x < Array.getLength(value); x++)
            {
               array.getArray().push(BuildScalar(true, Array.get(value, x)));
            }

            return array;
         }
      }

      if (primitives)
      {
         if (check.isPrimitive()) 
         { 
            check = normalizePrimitive(check); /* just in case, shouldn't be needed typically */
         }

         if (check == typeof(Boolean))
         {
            return SleepUtils.getScalar(  ((Boolean)value).booleanValue() ? 1 : 0 );
         }
         else if (check == typeof(Byte))
         {
            return SleepUtils.getScalar(  (int)( ((Byte)value).byteValue() )  );
         }
         else if (check == typeof(Character))
         {
            return SleepUtils.getScalar(  value.toString()  );
         }
         else if (check == typeof(Double))
         {
            return SleepUtils.getScalar(  ((Double)value).doubleValue()   );
         }
         else if (check == typeof(Float))
         {
            return SleepUtils.getScalar(  (double)( ((Float)value).floatValue() )  );
         }
         else if (check == typeof(Integer))
         {
            return SleepUtils.getScalar(  ((Integer)value).intValue()   );
         }
         else if (check == typeof(LongValue))
         {
            return SleepUtils.getScalar(  ((Long)value).longValue()   );
         }
      }

      if (check == typeof(StringJ) || check == typeof(System.String))
      {
         return SleepUtils.getScalar(value.toString());
      }
      else if (check == typeof(Scalar) || check == typeof(WatchScalar)) 
      {
         return (Scalar)value;
      }
      else 
      {
         return SleepUtils.getScalar(value);
      }
   }

   /** Determines the primitive type of the specified array.  Primitive Sleep values (int, long, double) will return the appropriate Number.TYPE class.  This is an important distinction as Double.TYPE != new Double().getClass() */
   public static Type getArrayType(Scalar value, Type defaultc)
   {
      if (value.getArray() != null && value.getArray().size() > 0 && (defaultc == null || (
         defaultc == typeof(java.lang.Object) || defaultc == typeof (System.Object))))
      {
          for (int x = 0; x < value.getArray().size(); x++)
          {
             if (value.getArray().getAt(x).getArray() != null)
             {
                return getArrayType(value.getArray().getAt(x), defaultc);
             }

             Class  elem  = value.getArray().getAt(x).getValue().getClass();
             Object tempo = value.getArray().getAt(x).objectValue();

             if (elem == DOUBLE_SCALAR)
             {
                return Double.TYPE;
             }
             else if (elem == INT_SCALAR)
             {
                return Integer.TYPE;
             }
             else if (elem == LONG_SCALAR)
             {
                return Long.TYPE;
             }
             else if (tempo != null)
             {
                return tempo.getClass();
             }
          }
      }

      return defaultc;
   }

   /** Standard method to handle a Java exception from a HOES call.  Basically this places the exception into Sleep's 
       throw mechanism and collects the stack frame. */
   public static void handleExceptionFromJava(java.lang.Throwable ex, ScriptEnvironment env, String description, int lineNumber)
   {
      if (ex != null)
      {                  
         env.flagError(ex);
 
         if (env.isThrownValue() && description != null && description.length() > 0)
         {
            env.getScriptInstance().recordStackFrame(description, lineNumber);
         }
      }
   }
}
}