﻿// PetaJson v0.5 - A simple but flexible Json library in a single .cs file.
// 
// Copyright (C) 2014 Topten Software (contact@toptensoftware.com) All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this product 
// except in compliance with the License. You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software distributed under the 
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, 
// either express or implied. See the License for the specific language governing permissions 
// and limitations under the License.

// Define PETAJSON_NO_DYNAMIC to disable Expando support
// Define PETAJSON_NO_EMIT to disable Reflection.Emit
// Define PETAJSON_NO_DATACONTRACT to disable support for [DataContract]/[DataMember]


#define PETAJSON_NO_DYNAMIC
#define PETAJSON_NO_EMIT
#define PETAJSON_NO_DATACONTRACT
// #define WITH_RWLOCK


using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Reflection;
using System.Globalization;
using System.Collections;
using System.Threading;


#if !PETAJSON_NO_DYNAMIC
using System.Dynamic;
#endif

#if !PETAJSON_NO_EMIT
using System.Reflection.Emit;
#endif

#if !PETAJSON_NO_DATACONTRACT
using System.Runtime.Serialization;
#endif



namespace CoreJWT.PetaJson
{

    // Func 
    public delegate TResult ReadCallback_t<out TResult>();
    public delegate TResult ReadCallback_t<in T, out TResult>(T arg);
    public delegate TResult ReadCallback_t<in T1, in T2, out TResult>(T1 arg1, T2 arg2);
    public delegate TResult ReadCallback_t<in T1, in T2, in T3, out TResult>(T1 arg1, T2 arg2, T3 arg3);


    // Action 
    public delegate void WriteCallback_t();
    public delegate void WriteCallback_t<in T>(T obj);
    public delegate void WriteCallback_t<in T1, in T2>(T1 arg1, T2 arg2);

    // Pass to format/write/parse functions to override defaults
    [Flags]
    public enum JsonOptions
    {
        None = 0,
        WriteWhitespace = 0x00000001,
        DontWriteWhitespace = 0x00000002,
        StrictParser = 0x00000004,
        NonStrictParser = 0x00000008,
    }


    // API
    public static class Json
    {
        static Json()
        {
            WriteWhitespaceDefault = true;
            StrictParserDefault = false;

#if !PETAJSON_NO_EMIT
            Json.SetFormatterResolver(Internal.Emit.MakeFormatter);
            Json.SetParserResolver(Internal.Emit.MakeParser);
            Json.SetIntoParserResolver(Internal.Emit.MakeIntoParser);
#endif
        }

        // Pretty format default
        public static bool WriteWhitespaceDefault
        {
            get;
            set;
        }

        // Strict parser
        public static bool StrictParserDefault
        {
            get;
            set;
        }

        // Write an object to a text writer
        public static void Write(TextWriter w, object o, JsonOptions options = JsonOptions.None)
        {
            Internal.Writer writer = new Internal.Writer(w, ResolveOptions(options));
            writer.WriteValue(o);
        }

        // Write an object to a file
        public static void WriteFile(string filename, object o, JsonOptions options = JsonOptions.None)
        {
            using (System.IO.FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (StreamWriter w = new StreamWriter(fs))
                {
                    Write(w, o, options);
                }
            }
        }

        // Format an object as a json string
        public static string Format(object o, JsonOptions options = JsonOptions.None)
        {
            StringWriter sw = new StringWriter();
            Internal.Writer writer = new Internal.Writer(sw, ResolveOptions(options));
            writer.WriteValue(o);
            return sw.ToString();
        }

        // Parse an object of specified type from a text reader
        public static object Parse(TextReader r, Type type, JsonOptions options = JsonOptions.None)
        {
            Internal.Reader reader = null;
            try
            {
                reader = new Internal.Reader(r, ResolveOptions(options));
                object retv = reader.Parse(type);
                reader.CheckEOF();
                return retv;
            }
            catch (Exception x)
            {
                JsonLineOffset loc = reader == null ? new JsonLineOffset() : reader.CurrentTokenPosition;
                Console.WriteLine("Exception thrown while parsing JSON at {0}, context:{1}\n{2}", loc, reader.Context, x.ToString());
                throw new JsonParseException(x, reader.Context, loc);
            }
        }

        // Parse an object of specified type from a text reader
        public static T Parse<T>(TextReader r, JsonOptions options = JsonOptions.None)
        {
            return (T)Parse(r, typeof(T), options);
        }

        // Parse from text reader into an already instantied object
        public static void ParseInto(TextReader r, Object into, JsonOptions options = JsonOptions.None)
        {
            if (into == null)
                throw new NullReferenceException();
            if (into.GetType().IsValueType())
                throw new InvalidOperationException("Can't ParseInto a value type");

            Internal.Reader reader = null;
            try
            {
                reader = new Internal.Reader(r, ResolveOptions(options));
                reader.ParseInto(into);
                reader.CheckEOF();
            }
            catch (Exception x)
            {
                JsonLineOffset loc = reader == null ? new JsonLineOffset() : reader.CurrentTokenPosition;
                Console.WriteLine("Exception thrown while parsing JSON at {0}, context:{1}\n{2}", loc, reader.Context, x.ToString());
                throw new JsonParseException(x, reader.Context, loc);
            }
        }


        // Parse an object of specified type from a file
        public static object ParseFile(string filename, Type type, JsonOptions options = JsonOptions.None)
        {
            using (System.IO.FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (StreamReader r = new StreamReader(fs))
                {
                    return Parse(r, type, options);
                }
            }
        }


        // Parse an object of specified type from a file
        public static T ParseFile<T>(string filename, JsonOptions options = JsonOptions.None)
        {
            using (System.IO.FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (StreamReader r = new StreamReader(fs))
                {
                    return Parse<T>(r, options);
                }
            }
        }


        // Parse from file into an already instantied object
        public static void ParseFileInto(string filename, Object into, JsonOptions options = JsonOptions.None)
        {
            using (System.IO.FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (StreamReader r = new StreamReader(fs))
                {
                    ParseInto(r, into, options);
                }
            }
        }


        // Parse an object from a string
        public static object Parse(string data, Type type, JsonOptions options = JsonOptions.None)
        {
            return Parse(new StringReader(data), type, options);
        }

        // Parse an object from a string
        public static T Parse<T>(string data, JsonOptions options = JsonOptions.None)
        {
            return (T)Parse<T>(new StringReader(data), options);
        }

        // Parse from string into an already instantiated object
        public static void ParseInto(string data, Object into, JsonOptions options = JsonOptions.None)
        {
            ParseInto(new StringReader(data), into, options);
        }

        // Create a clone of an object
        public static T Clone<T>(T source)
        {
            return (T)Reparse(source.GetType(), source);
        }

        // Create a clone of an object (untyped)
        public static object Clone(object source)
        {
            return Reparse(source.GetType(), source);
        }

        // Clone an object into another instance
        public static void CloneInto(object dest, object source)
        {
            ReparseInto(dest, source);
        }

        // Reparse an object by writing to a stream and re-reading (possibly
        // as a different type).
        public static object Reparse(Type type, object source)
        {
            if (source == null)
                return null;
            MemoryStream ms = new MemoryStream();
            try
            {
                // Write
                StreamWriter w = new StreamWriter(ms);
                Json.Write(w, source);
                w.Flush();

                // Read
                ms.Seek(0, SeekOrigin.Begin);
                StreamReader r = new StreamReader(ms);
                return Json.Parse(r, type);
            }
            finally
            {
                ms.Dispose();
            }
        }

        // Typed version of above
        public static T Reparse<T>(object source)
        {
            return (T)Reparse(typeof(T), source);
        }

        // Reparse one object into another object 
        public static void ReparseInto(object dest, object source)
        {
            MemoryStream ms = new MemoryStream();
            try
            {
                // Write
                StreamWriter w = new StreamWriter(ms);
                Json.Write(w, source);
                w.Flush();

                // Read
                ms.Seek(0, SeekOrigin.Begin);
                StreamReader r = new StreamReader(ms);
                Json.ParseInto(r, dest);
            }
            finally
            {
                ms.Dispose();
            }
        }

        // Register a callback that can format a value of a particular type into json
        public static void RegisterFormatter(Type type, WriteCallback_t<IJsonWriter, object> formatter)
        {
            Internal.Writer._formatters[type] = formatter;
        }

        // Typed version of above
        public static void RegisterFormatter<T>(WriteCallback_t<IJsonWriter, T> formatter)
        {
            RegisterFormatter(typeof(T), (w, o) => formatter(w, (T)o));
        }

        // Register a parser for a specified type
        public static void RegisterParser(Type type, ReadCallback_t<IJsonReader, Type, object> parser)
        {
            Internal.Reader._parsers.Set(type, parser);
        }

        // Register a typed parser
        public static void RegisterParser<T>(ReadCallback_t<IJsonReader, Type, T> parser)
        {
            RegisterParser(typeof(T), (r, t) => parser(r, t));
        }

        // Simpler version for simple types
        public static void RegisterParser(Type type, ReadCallback_t<object, object> parser)
        {
            RegisterParser(type, (r, t) => r.ReadLiteral(parser));
        }

        // Simpler and typesafe parser for simple types
        public static void RegisterParser<T>(ReadCallback_t<object, T> parser)
        {
            RegisterParser(typeof(T), literal => parser(literal));
        }

        // Register an into parser
        public static void RegisterIntoParser(Type type, WriteCallback_t<IJsonReader, object> parser)
        {
            Internal.Reader._intoParsers.Set(type, parser);
        }

        // Register an into parser
        public static void RegisterIntoParser<T>(WriteCallback_t<IJsonReader, object> parser)
        {
            RegisterIntoParser(typeof(T), parser);
        }

        // Register a factory for instantiating objects (typically abstract classes)
        // Callback will be invoked for each key in the dictionary until it returns an object
        // instance and which point it will switch to serialization using reflection
        public static void RegisterTypeFactory(Type type, ReadCallback_t<IJsonReader, string, object> factory)
        {
            Internal.Reader._typeFactories.Set(type, factory);
        }

        // Register a callback to provide a formatter for a newly encountered type
        public static void SetFormatterResolver(ReadCallback_t<Type, WriteCallback_t<IJsonWriter, object>> resolver)
        {
            Internal.Writer._formatterResolver = resolver;
        }

        // Register a callback to provide a parser for a newly encountered value type
        public static void SetParserResolver(ReadCallback_t<Type, ReadCallback_t<IJsonReader, Type, object>> resolver)
        {
            Internal.Reader._parserResolver = resolver;
        }

        // Register a callback to provide a parser for a newly encountered reference type
        public static void SetIntoParserResolver(ReadCallback_t<Type, WriteCallback_t<IJsonReader, object>> resolver)
        {
            Internal.Reader._intoParserResolver = resolver;
        }

        public static bool WalkPath(IDictionary<string, object> This, string Path, bool create, ReadCallback_t<IDictionary<string, object>, string, bool> leafCallback)
        {
            // Walk the path
            string[] parts = Path.Split('.');
            for (int i = 0; i < parts.Length - 1; i++)
            {
                object val;
                if (!This.TryGetValue(parts[i], out val))
                {
                    if (!create)
                        return false;

                    val = new Dictionary<string, object>();
                    This[parts[i]] = val;
                }
                This = (IDictionary<string, object>)val;
            }

            // Process the leaf
            return leafCallback(This, parts[parts.Length - 1]);
        }

        public static bool PathExists(IDictionary<string, object> This, string Path)
        {
            return WalkPath(This, Path, false, (dict, key) => dict.ContainsKey(key));
        }

        public static object GetPath(IDictionary<string, object> This, Type type, string Path, object def)
        {
            WalkPath(This, Path, false, (dict, key) =>
            {
                object val;
                if (dict.TryGetValue(key, out val))
                {
                    if (val == null)
                        def = val;
                    else if (type.IsAssignableFrom(val.GetType()))
                        def = val;
                    else
                        def = Reparse(type, val);
                }
                return true;
            });

            return def;
        }

        // Ensure there's an object of type T at specified path
        public static T GetObjectAtPath<T>(IDictionary<string, object> This, string Path) where T : class, new()
        {
            T retVal = null;
            WalkPath(This, Path, true, (dict, key) =>
            {
                object val;
                dict.TryGetValue(key, out val);
                retVal = val as T;
                if (retVal == null)
                {
                    retVal = val == null ? new T() : Reparse<T>(val);
                    dict[key] = retVal;
                }
                return true;
            });

            return retVal;
        }

        public static T GetPath<T>(IDictionary<string, object> This, string Path, T def = default(T))
        {
            return (T)GetPath(This, typeof(T), Path, def);
        }

        public static void SetPath(IDictionary<string, object> This, string Path, object value)
        {
            WalkPath(This, Path, true, (dict, key) => { dict[key] = value; return true; });
        }

        // Resolve passed options        
        static JsonOptions ResolveOptions(JsonOptions options)
        {
            JsonOptions resolved = JsonOptions.None;

            if ((options & (JsonOptions.WriteWhitespace | JsonOptions.DontWriteWhitespace)) != 0)
                resolved |= options & (JsonOptions.WriteWhitespace | JsonOptions.DontWriteWhitespace);
            else
                resolved |= WriteWhitespaceDefault ? JsonOptions.WriteWhitespace : JsonOptions.DontWriteWhitespace;

            if ((options & (JsonOptions.StrictParser | JsonOptions.NonStrictParser)) != 0)
                resolved |= options & (JsonOptions.StrictParser | JsonOptions.NonStrictParser);
            else
                resolved |= StrictParserDefault ? JsonOptions.StrictParser : JsonOptions.NonStrictParser;

            return resolved;
        }
    }

    // Called before loading via reflection
    public interface IJsonLoading
    {
        void OnJsonLoading(IJsonReader r);
    }

    // Called after loading via reflection
    public interface IJsonLoaded
    {
        void OnJsonLoaded(IJsonReader r);
    }

    // Called for each field while loading from reflection
    // Return true if handled
    public interface IJsonLoadField
    {
        bool OnJsonField(IJsonReader r, string key);
    }

    // Called when about to write using reflection

    public interface IJsonWriting
    {
        void OnJsonWriting(IJsonWriter w);
    }


    // Called after written using reflection
    public interface IJsonWritten
    {
        void OnJsonWritten(IJsonWriter w);
    }


    // Describes the current literal in the json stream
    public enum LiteralKind
    {
        None,
        String,
        Null,
        True,
        False,
        SignedInteger,
        UnsignedInteger,
        FloatingPoint,
    }


    // Passed to registered parsers
    public interface IJsonReader
    {
        object Parse(Type type);
        T Parse<T>();
        void ParseInto(object into);

        object ReadLiteral(ReadCallback_t<object, object> converter);
        void ParseDictionary(WriteCallback_t<string> callback);
        void ParseArray(WriteCallback_t callback);

        LiteralKind GetLiteralKind();
        string GetLiteralString();
        void NextToken();
    }

    // Passed to registered formatters

    public interface IJsonWriter
    {
        void WriteStringLiteral(string str);
        void WriteRaw(string str);
        void WriteArray(WriteCallback_t callback);
        void WriteDictionary(WriteCallback_t callback);
        void WriteValue(object value);
        void WriteElement();
        void WriteKey(string key);
        void WriteKeyNoEscaping(string key);
    }

    // Exception thrown for any parse error
    public class JsonParseException : Exception
    {
        public JsonParseException(Exception inner, string context, JsonLineOffset position) :
        base(string.Format("JSON parse error at {0}{1} - {2}", position, string.IsNullOrEmpty(context) ? "" : string.Format(", context {0}", context), inner.Message), inner)
        {
            Position = position;
            Context = context;
        }
        public JsonLineOffset Position;
        public string Context;
    }

    // Represents a line and character offset position in the source Json
    public struct JsonLineOffset
    {
        public int Line;
        public int Offset;
        public override string ToString()
        {
            return string.Format("line {0}, character {1}", Line + 1, Offset + 1);
        }
    }

    // Used to decorate fields and properties that should be serialized
    //
    // - [Json] on class or struct causes all public fields and properties to be serialized
    // - [Json] on a public or non-public field or property causes that member to be serialized
    // - [JsonExclude] on a field or property causes that field to be not serialized
    // - A class or struct with no [Json] attribute has all public fields/properties serialized
    // - A class or struct with no [Json] attribute but a [Json] attribute on one or more members only serializes those members
    //
    // Use [Json("keyname")] to explicitly specify the key to be used 
    // [Json] without the keyname will be serialized using the name of the member with the first letter lowercased.
    //
    // [Json(KeepInstance=true)] causes container/subobject types to be serialized into the existing member instance (if not null)
    //
    // You can also use the system supplied DataContract and DataMember attributes.  They'll only be used if there
    // are no PetaJson attributes on the class or it's members. You must specify DataContract on the type and
    // DataMember on any fields/properties that require serialization.  There's no need for exclude attribute.
    // When using DataMember, the name of the field or property is used as is - the first letter is left in upper case
    //
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field)]
    public class JsonAttribute : Attribute
    {
        public JsonAttribute()
        {
            _key = null;
        }

        public JsonAttribute(string key)
        {
            _key = key;
        }

        // Key used to save this field/property
        string _key;
        public string Key
        {
            get { return _key; }
        }

        // If true uses ParseInto to parse into the existing object instance
        // If false, creates a new instance as assigns it to the property
        public bool KeepInstance
        {
            get;
            set;
        }

        // If true, the property will be loaded, but not saved
        // Use to upgrade deprecated persisted settings, but not
        // write them back out again
        public bool Deprecated
        {
            get;
            set;
        }
    }

    // See comments for JsonAttribute above
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class JsonExcludeAttribute : Attribute
    {
        public JsonExcludeAttribute()
        {
        }
    }


    // Apply to enum values to specify which enum value to select
    // if the supplied json value doesn't match any.
    // If not found throws an exception
    // eg, any unknown values in the json will be mapped to Fruit.unknown
    //
    //   [JsonUnknown(Fruit.unknown)]
    //   enum Fruit
    //   {
    //      unknown,
    //      Apple,
    //      Pear,
    //   }
    [AttributeUsage(AttributeTargets.Enum)]
    public class JsonUnknownAttribute : Attribute
    {
        public JsonUnknownAttribute(object unknownValue)
        {
            UnknownValue = unknownValue;
        }

        public object UnknownValue
        {
            get;
            private set;
        }
    }

    namespace Internal
    {

        public enum Token
        {
            EOF,
            Identifier,
            Literal,
            OpenBrace,
            CloseBrace,
            OpenSquare,
            CloseSquare,
            Equal,
            Colon,
            SemiColon,
            Comma,
        }

        // Helper to create instances but include the type name in the thrown exception
        public static class DecoratingActivator
        {
            public static object CreateInstance(Type t)
            {
                try
                {
                    return Activator.CreateInstance(t);
                }
                catch (Exception x)
                {
                    throw new InvalidOperationException(string.Format("Failed to create instance of type '{0}'", t.FullName), x);
                }
            }
        }

        public class Reader : IJsonReader
        {
            static Reader()
            {
                // Setup default resolvers
                _parserResolver = ResolveParser;
                _intoParserResolver = ResolveIntoParser;

                ReadCallback_t<IJsonReader, Type, object> simpleConverter = (reader, type) =>
                {
                    return reader.ReadLiteral(literal => Convert.ChangeType(literal, type, CultureInfo.InvariantCulture));
                };

                ReadCallback_t<IJsonReader, Type, object> numberConverter = (reader, type) =>
                {
                    switch (reader.GetLiteralKind())
                    {
                        case LiteralKind.SignedInteger:
                        case LiteralKind.UnsignedInteger:
                        case LiteralKind.FloatingPoint:
                            object val = Convert.ChangeType(reader.GetLiteralString(), type, CultureInfo.InvariantCulture);
                            reader.NextToken();
                            return val;
                    }
                    throw new InvalidDataException("expected a numeric literal");
                };

                // Default type handlers
                _parsers.Set(typeof(string), simpleConverter);
                _parsers.Set(typeof(char), simpleConverter);
                _parsers.Set(typeof(bool), simpleConverter);
                _parsers.Set(typeof(byte), numberConverter);
                _parsers.Set(typeof(sbyte), numberConverter);
                _parsers.Set(typeof(short), numberConverter);
                _parsers.Set(typeof(ushort), numberConverter);
                _parsers.Set(typeof(int), numberConverter);
                _parsers.Set(typeof(uint), numberConverter);
                _parsers.Set(typeof(long), numberConverter);
                _parsers.Set(typeof(ulong), numberConverter);
                _parsers.Set(typeof(decimal), numberConverter);
                _parsers.Set(typeof(float), numberConverter);
                _parsers.Set(typeof(double), numberConverter);
                _parsers.Set(typeof(DateTime), (reader, type) =>
                {
                    return reader.ReadLiteral(literal => Utils.FromUnixMilliseconds((long)Convert.ChangeType(literal, typeof(long), CultureInfo.InvariantCulture)));
                });
                _parsers.Set(typeof(byte[]), (reader, type) =>
                {
                    return reader.ReadLiteral(literal => Convert.FromBase64String((string)Convert.ChangeType(literal, typeof(string), CultureInfo.InvariantCulture)));
                });
            }

            public Reader(TextReader r, JsonOptions options)
            {
                _tokenizer = new Tokenizer(r, options);
                _options = options;
            }

            Tokenizer _tokenizer;
            JsonOptions _options;
            List<string> _contextStack = new List<string>();

            public string Context
            {
                get
                {
                    return string.Join(".", _contextStack.ToArray());
                }
            }

            static WriteCallback_t<IJsonReader, object> ResolveIntoParser(Type type)
            {
                ReflectionInfo ri = ReflectionInfo.GetReflectionInfo(type);
                if (ri != null)
                    return ri.ParseInto;
                else
                    return null;
            }

            static ReadCallback_t<IJsonReader, Type, object> ResolveParser(Type type)
            {
                // See if the Type has a static parser method - T ParseJson(IJsonReader)
                System.Reflection.MethodInfo parseJson = ReflectionInfo.FindParseJson(type);
                if (parseJson != null)
                {
                    if (parseJson.GetParameters()[0].ParameterType == typeof(IJsonReader))
                    {
                        return (r, t) => parseJson.Invoke(null, new Object[] { r });
                    }
                    else
                    {
                        return (r, t) =>
                        {
                            if (r.GetLiteralKind() == LiteralKind.String)
                            {
                                object o = parseJson.Invoke(null, new Object[] { r.GetLiteralString() });
                                r.NextToken();
                                return o;
                            }
                            throw new InvalidDataException(string.Format("Expected string literal for type {0}", type.FullName));
                        };
                    }
                }

                return (r, t) =>
                {
                    object into = DecoratingActivator.CreateInstance(type);
                    r.ParseInto(into);
                    return into;
                };
            }

            public JsonLineOffset CurrentTokenPosition
            {
                get { return _tokenizer.CurrentTokenPosition; }
            }

            // ReadLiteral is implemented with a converter callback so that any
            // errors on converting to the target type are thrown before the tokenizer
            // is advanced to the next token.  This ensures error location is reported 
            // at the start of the literal, not the following token.
            public object ReadLiteral(ReadCallback_t<object, object> converter)
            {
                _tokenizer.Check(Token.Literal);
                object retv = converter(_tokenizer.LiteralValue);
                _tokenizer.NextToken();
                return retv;
            }

            public void CheckEOF()
            {
                _tokenizer.Check(Token.EOF);
            }

            public object Parse(Type type)
            {
                // Null?
                if (_tokenizer.CurrentToken == Token.Literal && _tokenizer.LiteralKind == LiteralKind.Null)
                {
                    _tokenizer.NextToken();
                    return null;
                }

                // Handle nullable types
                System.Type typeUnderlying = Nullable.GetUnderlyingType(type);
                if (typeUnderlying != null)
                    type = typeUnderlying;

                // See if we have a reader
                ReadCallback_t<IJsonReader, Type, object> parser;
                if (Reader._parsers.TryGetValue(type, out parser))
                {
                    return parser(this, type);
                }

                // See if we have factory
                ReadCallback_t<IJsonReader, string, object> factory;
                if (Reader._typeFactories.TryGetValue(type, out factory))
                {
                    // Try first without passing dictionary keys
                    object into = factory(this, null);
                    if (into == null)
                    {
                        // This is a awkward situation.  The factory requires a value from the dictionary
                        // in order to create the target object (typically an abstract class with the class
                        // kind recorded in the Json).  Since there's no guarantee of order in a json dictionary
                        // we can't assume the required key is first.
                        // So, create a bookmark on the tokenizer, read keys until the factory returns an
                        // object instance and then rewind the tokenizer and continue

                        // Create a bookmark so we can rewind
                        _tokenizer.CreateBookmark();

                        // Skip the opening brace
                        _tokenizer.Skip(Token.OpenBrace);

                        // First pass to work out type
                        ParseDictionaryKeys(key =>
                        {
                            // Try to instantiate the object
                            into = factory(this, key);
                            return into == null;
                        });

                        // Move back to start of the dictionary
                        _tokenizer.RewindToBookmark();

                        // Quit if still didn't get an object from the factory
                        if (into == null)
                            throw new InvalidOperationException("Factory didn't create object instance (probably due to a missing key in the Json)");
                    }

                    // Second pass
                    ParseInto(into);

                    // Done
                    return into;
                }

                // Do we already have an into parser?
                WriteCallback_t<IJsonReader, object> intoParser;
                if (Reader._intoParsers.TryGetValue(type, out intoParser))
                {
                    object into = DecoratingActivator.CreateInstance(type);
                    ParseInto(into);
                    return into;
                }

                // Enumerated type?
                if (type.IsEnum())
                {



                    if (Enumerable.Any(type.GetCustomAttributes(typeof(FlagsAttribute), false)))
                        return ReadLiteral(literal => {
                            try
                            {
                                return Enum.Parse(type, (string)literal);
                            }
                            catch
                            {
                                return Enum.ToObject(type, literal);
                            }
                        });
                    else
                        return ReadLiteral(literal => {

                            try
                            {
                                return Enum.Parse(type, (string)literal);
                            }
                            catch (Exception)
                            {
                                object attr = Enumerable.FirstOrDefault(type.GetCustomAttributes(typeof(JsonUnknownAttribute), false));
                                if (attr == null)
                                    throw;

                                return ((JsonUnknownAttribute)attr).UnknownValue;
                            }

                        });
                }

                // Array?
                if (type.IsArray && type.GetArrayRank() == 1)
                {
                    // First parse as a List<>
                    System.Type listType = typeof(List<>).MakeGenericType(type.GetElementType());
                    object list = DecoratingActivator.CreateInstance(listType);
                    ParseInto(list);

                    return listType.GetMethod("ToArray").Invoke(list, null);
                }

                // Convert interfaces to concrete types
                if (type.IsInterface())
                    type = Utils.ResolveInterfaceToClass(type);

                // Untyped dictionary?
                if (_tokenizer.CurrentToken == Token.OpenBrace && (type.IsAssignableFrom(typeof(IDictionary<string, object>))))
                {
#if !PETAJSON_NO_DYNAMIC
                    IDictionary<string, object> container = (new ExpandoObject()) as IDictionary<string, object>;
#else
                    Dictionary<string, object> container = new Dictionary<string, object>();
#endif
                    ParseDictionary(key =>
                    {
                        container[key] = Parse(typeof(Object));
                    });

                    return container;
                }

                // Untyped list?
                if (_tokenizer.CurrentToken == Token.OpenSquare && (type.IsAssignableFrom(typeof(List<object>))))
                {
                    List<object> container = new List<object>();
                    ParseArray(() =>
                    {
                        container.Add(Parse(typeof(Object)));
                    });
                    return container;
                }

                // Untyped literal?
                if (_tokenizer.CurrentToken == Token.Literal && type.IsAssignableFrom(_tokenizer.LiteralType))
                {
                    object lit = _tokenizer.LiteralValue;
                    _tokenizer.NextToken();
                    return lit;
                }

                // Call value type resolver
                if (type.IsValueType())
                {
                    ReadCallback_t<IJsonReader, System.Type, object> tp = _parsers.Get(type, () => _parserResolver(type));
                    if (tp != null)
                    {
                        return tp(this, type);
                    }
                }

                // Call reference type resolver
                if (type.IsClass() && type != typeof(object))
                {
                    object into = DecoratingActivator.CreateInstance(type);
                    ParseInto(into);
                    return into;
                }

                // Give up
                throw new InvalidDataException(string.Format("syntax error, unexpected token {0}", _tokenizer.CurrentToken));
            }

            // Parse into an existing object instance
            public void ParseInto(object into)
            {
                if (into == null)
                    return;

                if (_tokenizer.CurrentToken == Token.Literal && _tokenizer.LiteralKind == LiteralKind.Null)
                {
                    throw new InvalidOperationException("can't parse null into existing instance");
                    //return;
                }

                System.Type type = into.GetType();

                // Existing parse into handler?
                WriteCallback_t<IJsonReader, object> parseInto;
                if (_intoParsers.TryGetValue(type, out parseInto))
                {
                    parseInto(this, into);
                    return;
                }

                // Generic dictionary?
                System.Type dictType = Utils.FindGenericInterface(type, typeof(IDictionary<,>));
                if (dictType != null)
                {
                    // Get the key and value types
                    System.Type typeKey = dictType.GetGenericArguments()[0];
                    System.Type typeValue = dictType.GetGenericArguments()[1];

                    // Parse it
                    IDictionary dict = (IDictionary)into;
                    dict.Clear();
                    ParseDictionary(key =>
                    {
                        dict.Add(Convert.ChangeType(key, typeKey), Parse(typeValue));
                    });

                    return;
                }

                // Generic list
                System.Type listType = Utils.FindGenericInterface(type, typeof(IList<>));
                if (listType != null)
                {
                    // Get element type
                    System.Type typeElement = listType.GetGenericArguments()[0];

                    // Parse it
                    IList list = (IList)into;
                    list.Clear();
                    ParseArray(() =>
                    {
                        list.Add(Parse(typeElement));
                    });

                    return;
                }

                // Untyped dictionary
                IDictionary objDict = into as IDictionary;
                if (objDict != null)
                {
                    objDict.Clear();
                    ParseDictionary(key =>
                    {
                        objDict[key] = Parse(typeof(Object));
                    });
                    return;
                }

                // Untyped list
                IList objList = into as IList;
                if (objList != null)
                {
                    objList.Clear();
                    ParseArray(() =>
                    {
                        objList.Add(Parse(typeof(Object)));
                    });
                    return;
                }

                // Try to resolve a parser
                WriteCallback_t<IJsonReader, object> intoParser = _intoParsers.Get(type, () => _intoParserResolver(type));
                if (intoParser != null)
                {
                    intoParser(this, into);
                    return;
                }

                throw new InvalidOperationException(string.Format("Don't know how to parse into type '{0}'", type.FullName));
            }

            public T Parse<T>()
            {
                return (T)Parse(typeof(T));
            }

            public LiteralKind GetLiteralKind()
            {
                return _tokenizer.LiteralKind;
            }

            public string GetLiteralString()
            {
                return _tokenizer.String;
            }

            public void NextToken()
            {
                _tokenizer.NextToken();
            }

            // Parse a dictionary
            public void ParseDictionary(WriteCallback_t<string> callback)
            {
                _tokenizer.Skip(Token.OpenBrace);
                ParseDictionaryKeys(key => { callback(key); return true; });
                _tokenizer.Skip(Token.CloseBrace);
            }

            // Parse dictionary keys, calling callback for each one.  Continues until end of input
            // or when callback returns false
            private void ParseDictionaryKeys(ReadCallback_t<string, bool> callback)
            {
                // End?
                while (_tokenizer.CurrentToken != Token.CloseBrace)
                {
                    // Parse the key
                    string key = null;
                    if (_tokenizer.CurrentToken == Token.Identifier && (_options & JsonOptions.StrictParser) == 0)
                    {
                        key = _tokenizer.String;
                    }
                    else if (_tokenizer.CurrentToken == Token.Literal && _tokenizer.LiteralKind == LiteralKind.String)
                    {
                        key = (string)_tokenizer.LiteralValue;
                    }
                    else
                    {
                        throw new InvalidDataException("syntax error, expected string literal or identifier");
                    }
                    _tokenizer.NextToken();
                    _tokenizer.Skip(Token.Colon);

                    // Remember current position
                    JsonLineOffset pos = _tokenizer.CurrentTokenPosition;

                    // Call the callback, quit if cancelled
                    _contextStack.Add(key);
                    bool doDefaultProcessing = callback(key);
                    _contextStack.RemoveAt(_contextStack.Count - 1);
                    if (!doDefaultProcessing)
                        return;

                    // If the callback didn't read anything from the tokenizer, then skip it ourself
                    if (pos.Line == _tokenizer.CurrentTokenPosition.Line && pos.Offset == _tokenizer.CurrentTokenPosition.Offset)
                    {
                        Parse(typeof(object));
                    }

                    // Separating/trailing comma
                    if (_tokenizer.SkipIf(Token.Comma))
                    {
                        if ((_options & JsonOptions.StrictParser) != 0 && _tokenizer.CurrentToken == Token.CloseBrace)
                        {
                            throw new InvalidDataException("Trailing commas not allowed in strict mode");
                        }
                        continue;
                    }

                    // End
                    break;
                }
            }

            // Parse an array
            public void ParseArray(WriteCallback_t callback)
            {
                _tokenizer.Skip(Token.OpenSquare);

                int index = 0;
                while (_tokenizer.CurrentToken != Token.CloseSquare)
                {
                    _contextStack.Add(string.Format("[{0}]", index));
                    callback();
                    _contextStack.RemoveAt(_contextStack.Count - 1);

                    if (_tokenizer.SkipIf(Token.Comma))
                    {
                        if ((_options & JsonOptions.StrictParser) != 0 && _tokenizer.CurrentToken == Token.CloseSquare)
                        {
                            throw new InvalidDataException("Trailing commas not allowed in strict mode");
                        }
                        continue;
                    }
                    break;
                }

                _tokenizer.Skip(Token.CloseSquare);
            }

            // Yikes!
            public static ReadCallback_t<Type, WriteCallback_t<IJsonReader, object>> _intoParserResolver;
            public static ReadCallback_t<Type, ReadCallback_t<IJsonReader, Type, object>> _parserResolver;
            public static ThreadSafeCache<Type, ReadCallback_t<IJsonReader, Type, object>> _parsers = new ThreadSafeCache<Type, ReadCallback_t<IJsonReader, Type, object>>();
            public static ThreadSafeCache<Type, WriteCallback_t<IJsonReader, object>> _intoParsers = new ThreadSafeCache<Type, WriteCallback_t<IJsonReader, object>>();
            public static ThreadSafeCache<Type, ReadCallback_t<IJsonReader, string, object>> _typeFactories = new ThreadSafeCache<Type, ReadCallback_t<IJsonReader, string, object>>();
        }

        public class Writer : IJsonWriter
        {
            static Writer()
            {
                _formatterResolver = ResolveFormatter;

                // Register standard formatters
                _formatters.Add(typeof(string), (w, o) => w.WriteStringLiteral((string)o));
                _formatters.Add(typeof(char), (w, o) => w.WriteStringLiteral(((char)o).ToString()));
                _formatters.Add(typeof(bool), (w, o) => w.WriteRaw(((bool)o) ? "true" : "false"));
                WriteCallback_t<IJsonWriter, object> convertWriter = (w, o) => w.WriteRaw((string)Convert.ChangeType(o, typeof(string), System.Globalization.CultureInfo.InvariantCulture));
                _formatters.Add(typeof(int), convertWriter);
                _formatters.Add(typeof(uint), convertWriter);
                _formatters.Add(typeof(long), convertWriter);
                _formatters.Add(typeof(ulong), convertWriter);
                _formatters.Add(typeof(short), convertWriter);
                _formatters.Add(typeof(ushort), convertWriter);
                _formatters.Add(typeof(decimal), convertWriter);
                _formatters.Add(typeof(byte), convertWriter);
                _formatters.Add(typeof(sbyte), convertWriter);
                _formatters.Add(typeof(DateTime), (w, o) => convertWriter(w, Utils.ToUnixMilliseconds((DateTime)o)));
                _formatters.Add(typeof(float), (w, o) => w.WriteRaw(((float)o).ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
                _formatters.Add(typeof(double), (w, o) => w.WriteRaw(((double)o).ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
                _formatters.Add(typeof(byte[]), (w, o) =>
                {
                    w.WriteRaw("\"");
                    w.WriteRaw(Convert.ToBase64String((byte[])o));
                    w.WriteRaw("\"");
                });
            }

            public static ReadCallback_t<Type, WriteCallback_t<IJsonWriter, object>> _formatterResolver;
            public static Dictionary<Type, WriteCallback_t<IJsonWriter, object>> _formatters = new Dictionary<Type, WriteCallback_t<IJsonWriter, object>>();

            static WriteCallback_t<IJsonWriter, object> ResolveFormatter(Type type)
            {
                // Try `void FormatJson(IJsonWriter)`
                System.Reflection.MethodInfo formatJson = ReflectionInfo.FindFormatJson(type);
                if (formatJson != null)
                {
                    if (formatJson.ReturnType == typeof(void))
                        return (w, obj) => formatJson.Invoke(obj, new Object[] { w });
                    if (formatJson.ReturnType == typeof(string))
                        return (w, obj) => w.WriteStringLiteral((string)formatJson.Invoke(obj, new Object[] { }));
                }

                ReflectionInfo ri = ReflectionInfo.GetReflectionInfo(type);
                if (ri != null)
                    return ri.Write;
                else
                    return null;
            }

            public Writer(TextWriter w, JsonOptions options)
            {
                _writer = w;
                _atStartOfLine = true;
                _needElementSeparator = false;
                _options = options;
            }

            private TextWriter _writer;
            private int IndentLevel;
            private bool _atStartOfLine;
            private bool _needElementSeparator = false;
            private JsonOptions _options;
            private char _currentBlockKind = '\0';

            // Move to the next line
            public void NextLine()
            {
                if (_atStartOfLine)
                    return;

                if ((_options & JsonOptions.WriteWhitespace) != 0)
                {
                    WriteRaw("\n");
                    WriteRaw(new string('\t', IndentLevel));
                }
                _atStartOfLine = true;
            }

            // Start the next element, writing separators and white space
            void NextElement()
            {
                if (_needElementSeparator)
                {
                    WriteRaw(",");
                    NextLine();
                }
                else
                {
                    NextLine();
                    IndentLevel++;
                    WriteRaw(_currentBlockKind.ToString());
                    NextLine();
                }

                _needElementSeparator = true;
            }

            // Write next array element
            public void WriteElement()
            {
                if (_currentBlockKind != '[')
                    throw new InvalidOperationException("Attempt to write array element when not in array block");
                NextElement();
            }

            // Write next dictionary key
            public void WriteKey(string key)
            {
                if (_currentBlockKind != '{')
                    throw new InvalidOperationException("Attempt to write dictionary element when not in dictionary block");
                NextElement();
                WriteStringLiteral(key);
                WriteRaw(((_options & JsonOptions.WriteWhitespace) != 0) ? ": " : ":");
            }

            // Write an already escaped dictionary key
            public void WriteKeyNoEscaping(string key)
            {
                if (_currentBlockKind != '{')
                    throw new InvalidOperationException("Attempt to write dictionary element when not in dictionary block");
                NextElement();
                WriteRaw("\"");
                WriteRaw(key);
                WriteRaw("\"");
                WriteRaw(((_options & JsonOptions.WriteWhitespace) != 0) ? ": " : ":");
            }

            // Write anything
            public void WriteRaw(string str)
            {
                _atStartOfLine = false;
                _writer.Write(str);
            }

            static int IndexOfEscapeableChar(string str, int pos)
            {
                int length = str.Length;
                while (pos < length)
                {
                    char ch = str[pos];
                    if (ch == '\\' || ch == '/' || ch == '\"' || (ch >= 0 && ch <= 0x1f) || (ch >= 0x7f && ch <= 0x9f) || ch == 0x2028 || ch == 0x2029)
                        return pos;
                    pos++;
                }

                return -1;
            }

            public void WriteStringLiteral(string str)
            {
                _atStartOfLine = false;
                if (str == null)
                {
                    _writer.Write("null");
                    return;
                }
                _writer.Write("\"");

                int pos = 0;
                int escapePos;
                while ((escapePos = IndexOfEscapeableChar(str, pos)) >= 0)
                {
                    if (escapePos > pos)
                        _writer.Write(str.Substring(pos, escapePos - pos));

                    switch (str[escapePos])
                    {
                        case '\"': _writer.Write("\\\""); break;
                        case '\\': _writer.Write("\\\\"); break;
                        case '/': _writer.Write("\\/"); break;
                        case '\b': _writer.Write("\\b"); break;
                        case '\f': _writer.Write("\\f"); break;
                        case '\n': _writer.Write("\\n"); break;
                        case '\r': _writer.Write("\\r"); break;
                        case '\t': _writer.Write("\\t"); break;
                        default:
                            _writer.Write(string.Format("\\u{0:x4}", (int)str[escapePos]));
                            break;
                    }

                    pos = escapePos + 1;
                }


                if (str.Length > pos)
                    _writer.Write(str.Substring(pos));
                _writer.Write("\"");
            }

            // Write an array or dictionary block
            private void WriteBlock(string open, string close, WriteCallback_t callback)
            {
                char prevBlockKind = _currentBlockKind;
                _currentBlockKind = open[0];

                bool didNeedElementSeparator = _needElementSeparator;
                _needElementSeparator = false;

                callback();

                if (_needElementSeparator)
                {
                    IndentLevel--;
                    NextLine();
                }
                else
                {
                    WriteRaw(open);
                }
                WriteRaw(close);

                _needElementSeparator = didNeedElementSeparator;
                _currentBlockKind = prevBlockKind;
            }

            // Write an array
            public void WriteArray(WriteCallback_t callback)
            {
                WriteBlock("[", "]", callback);
            }

            // Write a dictionary
            public void WriteDictionary(WriteCallback_t callback)
            {
                WriteBlock("{", "}", callback);
            }

            // Write any value
            public void WriteValue(object value)
            {
                _atStartOfLine = false;

                // Special handling for null
                if (value == null)
                {
                    _writer.Write("null");
                    return;
                }

                System.Type type = value.GetType();

                // Handle nullable types
                System.Type typeUnderlying = Nullable.GetUnderlyingType(type);
                if (typeUnderlying != null)
                    type = typeUnderlying;

                // Look up type writer
                WriteCallback_t<IJsonWriter, object> typeWriter;
                if (_formatters.TryGetValue(type, out typeWriter))
                {
                    // Write it
                    typeWriter(this, value);
                    return;
                }

                // Enumerated type?
                if (type.IsEnum())
                {
                    if (Enumerable.Any(type.GetCustomAttributes(typeof(FlagsAttribute), false)))
                        WriteRaw(Convert.ToUInt32(value).ToString(CultureInfo.InvariantCulture));
                    else
                        WriteStringLiteral(value.ToString());
                    return;
                }

                // Dictionary?
                System.Collections.IDictionary d = value as System.Collections.IDictionary;
                if (d != null)
                {
                    WriteDictionary(() =>
                    {
                        foreach (object key in d.Keys)
                        {
                            WriteKey(key.ToString());
                            WriteValue(d[key]);
                        }
                    });
                    return;
                }

                // Dictionary?
                IDictionary<string, object> dso = value as IDictionary<string, object>;
                if (dso != null)
                {
                    WriteDictionary(() =>
                    {
                        foreach (string key in dso.Keys)
                        {
                            WriteKey(key.ToString());
                            WriteValue(dso[key]);
                        }
                    });
                    return;
                }

                // Array?
                System.Collections.IEnumerable e = value as System.Collections.IEnumerable;
                if (e != null)
                {
                    WriteArray(() =>
                    {
                        foreach (object i in e)
                        {
                            WriteElement();
                            WriteValue(i);
                        }
                    });
                    return;
                }

                // Resolve a formatter
                WriteCallback_t<IJsonWriter, object> formatter = _formatterResolver(type);
                if (formatter != null)
                {
                    _formatters[type] = formatter;
                    formatter(this, value);
                    return;
                }

                // Give up
                throw new InvalidDataException(string.Format("Don't know how to write '{0}' to json", value.GetType()));
            }
        }

        // Information about a field or property found through reflection
        public class JsonMemberInfo
        {
            // The Json key for this member
            public string JsonKey;

            // True if should keep existing instance (reference types only)
            public bool KeepInstance;

            // True if deprecated
            public bool Deprecated;



            // Reflected member info
            MemberInfo _mi;
            public MemberInfo Member
            {
                get { return _mi; }
                set
                {
                    // Store it
                    _mi = value;

                    // Also create getters and setters
                    if (_mi is PropertyInfo)
                    {
                        // GetValue = (obj) => ((PropertyInfo)_mi).GetValue(obj, null);
                        // SetValue = (obj, val) => ((PropertyInfo)_mi).SetValue(obj, val, null);

                        PropertyInfo pi = (PropertyInfo)_mi;

                        // Property can be readonly or writeonly
                        if (pi.CanRead)
                            GetValue = (obj) => pi.GetValue(obj, null);
                        else
                            GetValue = (obj) => {
                                bool canBeNull = !pi.PropertyType.IsValueType() || (Nullable.GetUnderlyingType(pi.PropertyType) != null);
                                if (canBeNull)
                                    return null;

                                if (pi.PropertyType.IsValueType())
                                    return Activator.CreateInstance(pi.PropertyType);

                                // Will throw in that case. 
                                return null;
                            };

                        if (pi.CanWrite)
                            SetValue = (obj, val) => pi.SetValue(obj, val, null);
                        else // Don't throw if this is a readonly property...
                            SetValue = (obj, val) => { };
                    }
                    else
                    {
                        GetValue = ((FieldInfo)_mi).GetValue;
                        SetValue = ((FieldInfo)_mi).SetValue;
                    }
                }
            }

            // Member type
            public Type MemberType
            {
                get
                {
                    if (Member is PropertyInfo)
                    {
                        return ((PropertyInfo)Member).PropertyType;
                    }
                    else
                    {
                        return ((FieldInfo)Member).FieldType;
                    }
                }
            }

            // Get/set helpers
            public WriteCallback_t<object, object> SetValue;
            public ReadCallback_t<object, object> GetValue;
        }

        // Stores reflection info about a type
        public class ReflectionInfo
        {
            // List of members to be serialized
            public List<JsonMemberInfo> Members;

            // Cache of these ReflectionInfos's
            static ThreadSafeCache<Type, ReflectionInfo> _cache = new ThreadSafeCache<Type, ReflectionInfo>();

            public static MethodInfo FindFormatJson(Type type)
            {
                if (type.IsValueType())
                {
                    // Try `void FormatJson(IJsonWriter)`
                    System.Reflection.MethodInfo formatJson = type.GetMethod("FormatJson", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(IJsonWriter) }, null);
                    if (formatJson != null && formatJson.ReturnType == typeof(void))
                        return formatJson;

                    // Try `string FormatJson()`
                    formatJson = type.GetMethod("FormatJson", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { }, null);
                    if (formatJson != null && formatJson.ReturnType == typeof(string))
                        return formatJson;
                }
                return null;
            }

            public static MethodInfo FindParseJson(Type type)
            {
                // Try `T ParseJson(IJsonReader)`
                System.Reflection.MethodInfo parseJson = type.GetMethod("ParseJson", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(IJsonReader) }, null);
                if (parseJson != null && parseJson.ReturnType == type)
                    return parseJson;

                // Try `T ParseJson(string)`
                parseJson = type.GetMethod("ParseJson", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(string) }, null);
                if (parseJson != null && parseJson.ReturnType == type)
                    return parseJson;

                return null;
            }

            // Write one of these types
            public void Write(IJsonWriter w, object val)
            {
                w.WriteDictionary(() =>
                {
                    IJsonWriting writing = val as IJsonWriting;
                    if (writing != null)
                        writing.OnJsonWriting(w);

                    foreach (JsonMemberInfo jmi in Members)
                    {

                        if (!jmi.Deprecated)
                        {
                            w.WriteKeyNoEscaping(jmi.JsonKey);
                            w.WriteValue(jmi.GetValue(val));
                        } // End if(!jmi.Deprecated)

                    } // Next jmi 

                    IJsonWritten written = val as IJsonWritten;
                    if (written != null)
                        written.OnJsonWritten(w);
                });
            }

            // Read one of these types.
            // NB: Although PetaJson.JsonParseInto only works on reference type, when using reflection
            //     it also works for value types so we use the one method for both
            public void ParseInto(IJsonReader r, object into)
            {
                IJsonLoading loading = into as IJsonLoading;
                if (loading != null)
                    loading.OnJsonLoading(r);

                r.ParseDictionary(key =>
                {
                    ParseFieldOrProperty(r, into, key);
                });

                IJsonLoaded loaded = into as IJsonLoaded;
                if (loaded != null)
                    loaded.OnJsonLoaded(r);
            }

            // The member info is stored in a list (as opposed to a dictionary) so that
            // the json is written in the same order as the fields/properties are defined
            // On loading, we assume the fields will be in the same order, but need to
            // handle if they're not.  This function performs a linear search, but
            // starts after the last found item as an optimization that should work
            // most of the time.
            int _lastFoundIndex = 0;
            bool FindMemberInfo(string name, out JsonMemberInfo found)
            {
                for (int i = 0; i < Members.Count; i++)
                {
                    int index = (i + _lastFoundIndex) % Members.Count;
                    JsonMemberInfo jmi = Members[index];
                    if (jmi.JsonKey == name)
                    {
                        _lastFoundIndex = index;
                        found = jmi;
                        return true;
                    }
                }
                found = null;
                return false;
            }


            // Parse a value from IJsonReader into an object instance
            public void ParseFieldOrProperty(IJsonReader r, object into, string key)
            {
                // IJsonLoadField
                IJsonLoadField lf = into as IJsonLoadField;
                if (lf != null && lf.OnJsonField(r, key))
                    return;

                // Find member
                JsonMemberInfo jmi;
                if (FindMemberInfo(key, out jmi))
                {
                    // Try to keep existing instance
                    if (jmi.KeepInstance)
                    {
                        object subInto = jmi.GetValue(into);
                        if (subInto != null)
                        {
                            r.ParseInto(subInto);
                            return;
                        }
                    }

                    // Parse and set
                    object val = r.Parse(jmi.MemberType);
                    jmi.SetValue(into, val);
                    return;
                }
            }

            // Get the reflection info for a specified type
            public static ReflectionInfo GetReflectionInfo(Type type)
            {
                // Check cache
                return _cache.Get(type, () =>
                {
                    IEnumerable<MemberInfo> allMembers = Utils.GetAllFieldsAndProperties(type);

                    // Does type have a [Json] attribute
                    bool typeMarked = Enumerable.Any(Enumerable.OfType<JsonAttribute>(type.GetCustomAttributes(typeof(JsonAttribute), true)));

                    // Do any members have a [Json] attribute
                    bool anyFieldsMarked = Enumerable.Any(allMembers, x => Enumerable.Any(Enumerable.OfType<JsonAttribute>(x.GetCustomAttributes(typeof(JsonAttribute), false))));


#if !PETAJSON_NO_DATACONTRACT
                        // Try with DataContract and friends
                        if (!typeMarked && !anyFieldsMarked && Enumerable.Any(Enumerable.OfType<DataContractAttribute>(type.GetCustomAttributes(typeof(DataContractAttribute), true))))
                        {
                            ReflectionInfo ri = CreateReflectionInfo(type, mi =>
                                {
                                    // Get attributes
                                    object[] attr = Enumerable.FirstOrDefault(Enumerable.OfType<DataMemberAttribute>(mi.GetCustomAttributes(typeof(DataMemberAttribute), false)));
                                    if (attr != null)
                                    {
                                        return new JsonMemberInfo()
                                        {
                                            Member = mi,
                                            JsonKey = attr.Name ?? mi.Name,     // No lower case first letter if using DataContract/Member
                                        };
                                    }

                                    return null;
                                });

                            ri.Members.Sort((a, b) => String.CompareOrdinal(a.JsonKey, b.JsonKey));    // Match DataContractJsonSerializer
                            return ri;
                        }
#endif
                    {
                        // Should we serialize all public methods?
                        bool serializeAllPublics = typeMarked || !anyFieldsMarked;

                        // Build 
                        ReflectionInfo ri = CreateReflectionInfo(type, mi =>
                        {
                            // Explicitly excluded?
                            if (Enumerable.Any(mi.GetCustomAttributes(typeof(JsonExcludeAttribute), false)))
                                return null;

                            // Get attributes
                            JsonAttribute attr = Enumerable.FirstOrDefault(Enumerable.OfType<JsonAttribute>(mi.GetCustomAttributes(typeof(JsonAttribute), false)));
                            if (attr != null)
                            {
                                return new JsonMemberInfo()
                                {
                                    Member = mi,
                                    JsonKey = attr.Key ?? mi.Name.Substring(0, 1).ToLower() + mi.Name.Substring(1),
                                    KeepInstance = attr.KeepInstance,
                                    Deprecated = attr.Deprecated,
                                };
                            }

                            // Serialize all publics?
                            if (serializeAllPublics && Utils.IsPublic(mi))
                            {
                                return new JsonMemberInfo()
                                {
                                    Member = mi,
                                    JsonKey = mi.Name.Substring(0, 1).ToLower() + mi.Name.Substring(1),
                                };
                            }

                            return null;
                        });
                        return ri;
                    }
                });
            }

            public static ReflectionInfo CreateReflectionInfo(Type type, ReadCallback_t<MemberInfo, JsonMemberInfo> callback)
            {
                // Work out properties and fields

                List<JsonMemberInfo> members = new List<JsonMemberInfo>();
                foreach (MemberInfo thisMember in Utils.GetAllFieldsAndProperties(type))
                {
                    JsonMemberInfo mi = callback(thisMember);
                    if (mi != null)
                        members.Add(mi);
                }

                // Anything with KeepInstance must be a reference type
                JsonMemberInfo invalid = Enumerable.FirstOrDefault(members, x => x.KeepInstance && x.MemberType.IsValueType());
                if (invalid != null)
                {
                    throw new InvalidOperationException(string.Format("KeepInstance=true can only be applied to reference types ({0}.{1})", type.FullName, invalid.Member));
                }

                // Must have some members
                if (!Enumerable.Any(members))
                    return null;

                // Create reflection info
                return new ReflectionInfo() { Members = members };
            }
        }

        public class ThreadSafeCache<TKey, TValue>
        {

            Dictionary<TKey, TValue> _map = new Dictionary<TKey, TValue>();
#if WITH_RWLOCK
            ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
#else
            object _lock = new object();
#endif


            public ThreadSafeCache()
            { }

            public TValue Get(TKey key, ReadCallback_t<TValue> createIt)
            {
                // Check if already exists
#if WITH_RWLOCK
                _lock.EnterReadLock();
#else
                lock (_lock)
                {
#endif

                    try
                    {
                        TValue val;
                        if (_map.TryGetValue(key, out val))
                            return val;
                    }
                    finally
                    {
#if WITH_RWLOCK
                    _lock.ExitReadLock();
#endif

                    }

#if !WITH_RWLOCK
                }
#endif


                // Nope, take lock and try again
#if WITH_RWLOCK
                _lock.EnterWriteLock();
#else
                lock (_lock)
                {
#endif

                    try
                    {
                        // Check again before creating it
                        TValue val;
                        if (!_map.TryGetValue(key, out val))
                        {
                            // Store the new one
                            val = createIt();
                            _map[key] = val;
                        }
                        return val;
                    }
                    finally
                    {
#if WITH_RWLOCK
                    _lock.ExitWriteLock();
#endif

                    }

#if !WITH_RWLOCK
                }
#endif


            }

            public bool TryGetValue(TKey key, out TValue val)
            {
#if WITH_RWLOCK
                _lock.EnterReadLock();
#else
                lock (_lock)
                {
#endif
                    try
                    {
                        return _map.TryGetValue(key, out val);
                    }
                    finally
                    {
#if WITH_RWLOCK
                    _lock.ExitReadLock();
#endif
                    }
#if !WITH_RWLOCK
                }
#endif

            }

            public void Set(TKey key, TValue value)
            {
#if WITH_RWLOCK
                _lock.EnterWriteLock();
#else
                lock (_lock)
                {
#endif
                    try
                    {
                        _map[key] = value;
                    }
                    finally
                    {
#if WITH_RWLOCK
                    _lock.ExitWriteLock();
#endif
                    }

#if !WITH_RWLOCK
                }
#endif


            }

        }

        internal static class Utils
        {
            // Get all fields and properties of a type
            public static IEnumerable<MemberInfo> GetAllFieldsAndProperties(Type t)
            {
                if (t == null)
                {
                    List<MemberInfo> lsMemberInfo = new List<MemberInfo>();
                    return lsMemberInfo;
                }

                BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

                List<MemberInfo> fieldsAndProps = new List<MemberInfo>();


                foreach (MemberInfo x in t.GetMembers(flags))
                {
                    if (x is FieldInfo || x is PropertyInfo)
                        fieldsAndProps.Add(x);
                }

                IEnumerable<MemberInfo> baseFieldsAndProps = GetAllFieldsAndProperties(t.BaseType());
                fieldsAndProps.AddRange(baseFieldsAndProps);

                return fieldsAndProps;
            }

            public static Type FindGenericInterface(Type type, Type tItf)
            {
                foreach (System.Type t in type.GetInterfaces())
                {
                    // Is this a generic list?
                    if (t.IsGenericType() && t.GetGenericTypeDefinition() == tItf)
                        return t;
                }

                return null;
            }

            public static bool IsPublic(MemberInfo mi)
            {
                // Public field
                FieldInfo fi = mi as FieldInfo;
                if (fi != null)
                    return fi.IsPublic;

                // Public property
                // (We only check the get method so we can work with anonymous types)
                PropertyInfo pi = mi as PropertyInfo;
                if (pi != null)
                {
                    MethodInfo gm = pi.GetGetMethod(true);
                    return (gm != null && gm.IsPublic);
                }

                return false;
            }

            public static Type ResolveInterfaceToClass(Type tItf)
            {
                // Generic type
                if (tItf.IsGenericType())
                {
                    System.Type genDef = tItf.GetGenericTypeDefinition();

                    // IList<> -> List<>
                    if (genDef == typeof(IList<>))
                    {
                        return typeof(List<>).MakeGenericType(tItf.GetGenericArguments());
                    }

                    // IDictionary<string,> -> Dictionary<string,>
                    if (genDef == typeof(IDictionary<,>) && tItf.GetGenericArguments()[0] == typeof(string))
                    {
                        return typeof(Dictionary<,>).MakeGenericType(tItf.GetGenericArguments());
                    }
                }

                // IEnumerable -> List<object>
                if (tItf == typeof(IEnumerable))
                    return typeof(List<object>);

                // IDicitonary -> Dictionary<string,object>
                if (tItf == typeof(IDictionary))
                    return typeof(Dictionary<string, object>);
                return tItf;
            }

            public static long ToUnixMilliseconds(DateTime This)
            {
                return (long)This.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
            }

            public static DateTime FromUnixMilliseconds(long timeStamp)
            {
                return new DateTime(1970, 1, 1).AddMilliseconds(timeStamp);
            }

        }

        public class Tokenizer
        {
            public Tokenizer(TextReader r, JsonOptions options)
            {
                _underlying = r;
                _options = options;
                FillBuffer();
                NextChar();
                NextToken();
            }

            private JsonOptions _options;
            private StringBuilder _sb = new StringBuilder();
            private TextReader _underlying;
            private char[] _buf = new char[4096];
            private int _pos;
            private int _bufUsed;
            private StringBuilder _rewindBuffer;
            private int _rewindBufferPos;
            private JsonLineOffset _currentCharPos;
            private char _currentChar;
            private Stack<ReaderState> _bookmarks = new Stack<ReaderState>();

            public JsonLineOffset CurrentTokenPosition;
            public Token CurrentToken;
            public LiteralKind LiteralKind;
            public string String;

            public object LiteralValue
            {
                get
                {
                    if (CurrentToken != Token.Literal)
                        throw new InvalidOperationException("token is not a literal");
                    switch (LiteralKind)
                    {
                        case LiteralKind.Null: return null;
                        case LiteralKind.False: return false;
                        case LiteralKind.True: return true;
                        case LiteralKind.String: return String;
                        case LiteralKind.SignedInteger: return long.Parse(String, CultureInfo.InvariantCulture);
                        case LiteralKind.UnsignedInteger: return ulong.Parse(String, CultureInfo.InvariantCulture);
                        case LiteralKind.FloatingPoint: return double.Parse(String, CultureInfo.InvariantCulture);
                    }
                    return null;
                }
            }

            public Type LiteralType
            {
                get
                {
                    if (CurrentToken != Token.Literal)
                        throw new InvalidOperationException("token is not a literal");
                    switch (LiteralKind)
                    {
                        case LiteralKind.Null: return typeof(Object);
                        case LiteralKind.False: return typeof(Boolean);
                        case LiteralKind.True: return typeof(Boolean);
                        case LiteralKind.String: return typeof(string);
                        case LiteralKind.SignedInteger: return typeof(long);
                        case LiteralKind.UnsignedInteger: return typeof(ulong);
                        case LiteralKind.FloatingPoint: return typeof(double);
                    }

                    return null;
                }
            }

            // This object represents the entire state of the reader and is used for rewind
            struct ReaderState
            {
                public ReaderState(Tokenizer tokenizer)
                {
                    _currentCharPos = tokenizer._currentCharPos;
                    _currentChar = tokenizer._currentChar;
                    _string = tokenizer.String;
                    _literalKind = tokenizer.LiteralKind;
                    _rewindBufferPos = tokenizer._rewindBufferPos;
                    _currentTokenPos = tokenizer.CurrentTokenPosition;
                    _currentToken = tokenizer.CurrentToken;
                }

                public void Apply(Tokenizer tokenizer)
                {
                    tokenizer._currentCharPos = _currentCharPos;
                    tokenizer._currentChar = _currentChar;
                    tokenizer._rewindBufferPos = _rewindBufferPos;
                    tokenizer.CurrentToken = _currentToken;
                    tokenizer.CurrentTokenPosition = _currentTokenPos;
                    tokenizer.String = _string;
                    tokenizer.LiteralKind = _literalKind;
                }

                private JsonLineOffset _currentCharPos;
                private JsonLineOffset _currentTokenPos;
                private char _currentChar;
                private Token _currentToken;
                private LiteralKind _literalKind;
                private string _string;
                private int _rewindBufferPos;
            }

            // Create a rewind bookmark
            public void CreateBookmark()
            {
                _bookmarks.Push(new ReaderState(this));
                if (_rewindBuffer == null)
                {
                    _rewindBuffer = new StringBuilder();
                    _rewindBufferPos = 0;
                }
            }

            // Discard bookmark
            public void DiscardBookmark()
            {
                _bookmarks.Pop();
                if (_bookmarks.Count == 0)
                {
                    _rewindBuffer = null;
                    _rewindBufferPos = 0;
                }
            }

            // Rewind to a bookmark
            public void RewindToBookmark()
            {
                _bookmarks.Pop().Apply(this);
            }

            // Fill buffer by reading from underlying TextReader
            void FillBuffer()
            {
                _bufUsed = _underlying.Read(_buf, 0, _buf.Length);
                _pos = 0;
            }

            // Get the next character from the input stream
            // (this function could be extracted into a few different methods, but is mostly inlined
            //  for performance - yes it makes a difference)
            public char NextChar()
            {
                if (_rewindBuffer == null)
                {
                    if (_pos >= _bufUsed)
                    {
                        if (_bufUsed > 0)
                        {
                            FillBuffer();
                        }
                        if (_bufUsed == 0)
                        {
                            return _currentChar = '\0';
                        }
                    }

                    // Next
                    _currentCharPos.Offset++;
                    return _currentChar = _buf[_pos++];
                }

                if (_rewindBufferPos < _rewindBuffer.Length)
                {
                    _currentCharPos.Offset++;
                    return _currentChar = _rewindBuffer[_rewindBufferPos++];
                }
                else
                {
                    if (_pos >= _bufUsed && _bufUsed > 0)
                        FillBuffer();

                    _currentChar = _bufUsed == 0 ? '\0' : _buf[_pos++];
                    _rewindBuffer.Append(_currentChar);
                    _rewindBufferPos++;
                    _currentCharPos.Offset++;
                    return _currentChar;
                }
            }

            // Read the next token from the input stream
            // (Mostly inline for performance)
            public void NextToken()
            {
                while (true)
                {
                    // Skip whitespace and handle line numbers
                    while (true)
                    {
                        if (_currentChar == '\r')
                        {
                            if (NextChar() == '\n')
                            {
                                NextChar();
                            }
                            _currentCharPos.Line++;
                            _currentCharPos.Offset = 0;
                        }
                        else if (_currentChar == '\n')
                        {
                            if (NextChar() == '\r')
                            {
                                NextChar();
                            }
                            _currentCharPos.Line++;
                            _currentCharPos.Offset = 0;
                        }
                        else if (_currentChar == ' ')
                        {
                            NextChar();
                        }
                        else if (_currentChar == '\t')
                        {
                            NextChar();
                        }
                        else
                            break;
                    }

                    // Remember position of token
                    CurrentTokenPosition = _currentCharPos;

                    // Handle common characters first
                    switch (_currentChar)
                    {
                        case '/':
                            // Comments not support in strict mode
                            if ((_options & JsonOptions.StrictParser) != 0)
                            {
                                throw new InvalidDataException(string.Format("syntax error, unexpected character '{0}'", _currentChar));
                            }

                            // Process comment
                            NextChar();
                            switch (_currentChar)
                            {
                                case '/':
                                    NextChar();
                                    while (_currentChar != '\0' && _currentChar != '\r' && _currentChar != '\n')
                                    {
                                        NextChar();
                                    }
                                    break;

                                case '*':
                                    bool endFound = false;
                                    while (!endFound && _currentChar != '\0')
                                    {
                                        if (_currentChar == '*')
                                        {
                                            NextChar();
                                            if (_currentChar == '/')
                                            {
                                                endFound = true;
                                            }
                                        }
                                        NextChar();
                                    }
                                    break;

                                default:
                                    throw new InvalidDataException("syntax error, unexpected character after slash");
                            }
                            continue;

                        case '\"':
                        case '\'':
                            {
                                _sb.Length = 0;
                                char quoteKind = _currentChar;
                                NextChar();
                                while (_currentChar != '\0')
                                {
                                    if (_currentChar == '\\')
                                    {
                                        NextChar();
                                        char escape = _currentChar;
                                        switch (escape)
                                        {
                                            case '\"': _sb.Append('\"'); break;
                                            case '\\': _sb.Append('\\'); break;
                                            case '/': _sb.Append('/'); break;
                                            case 'b': _sb.Append('\b'); break;
                                            case 'f': _sb.Append('\f'); break;
                                            case 'n': _sb.Append('\n'); break;
                                            case 'r': _sb.Append('\r'); break;
                                            case 't': _sb.Append('\t'); break;
                                            case 'u':
                                                System.Text.StringBuilder sbHex = new StringBuilder();
                                                for (int i = 0; i < 4; i++)
                                                {
                                                    NextChar();
                                                    sbHex.Append(_currentChar);
                                                }
                                                _sb.Append((char)Convert.ToUInt16(sbHex.ToString(), 16));
                                                break;

                                            default:
                                                throw new InvalidDataException(string.Format("Invalid escape sequence in string literal: '\\{0}'", _currentChar));
                                        }
                                    }
                                    else if (_currentChar == quoteKind)
                                    {
                                        String = _sb.ToString();
                                        CurrentToken = Token.Literal;
                                        LiteralKind = LiteralKind.String;
                                        NextChar();
                                        return;
                                    }
                                    else
                                    {
                                        _sb.Append(_currentChar);
                                    }

                                    NextChar();
                                }
                                throw new InvalidDataException("syntax error, unterminated string literal");
                            }

                        case '{': CurrentToken = Token.OpenBrace; NextChar(); return;
                        case '}': CurrentToken = Token.CloseBrace; NextChar(); return;
                        case '[': CurrentToken = Token.OpenSquare; NextChar(); return;
                        case ']': CurrentToken = Token.CloseSquare; NextChar(); return;
                        case '=': CurrentToken = Token.Equal; NextChar(); return;
                        case ':': CurrentToken = Token.Colon; NextChar(); return;
                        case ';': CurrentToken = Token.SemiColon; NextChar(); return;
                        case ',': CurrentToken = Token.Comma; NextChar(); return;
                        case '\0': CurrentToken = Token.EOF; return;
                    }

                    // Number?
                    if (char.IsDigit(_currentChar) || _currentChar == '-')
                    {
                        TokenizeNumber();
                        return;
                    }

                    // Identifier?  (checked for after everything else as identifiers are actually quite rare in valid json)
                    if (Char.IsLetter(_currentChar) || _currentChar == '_' || _currentChar == '$')
                    {
                        // Find end of identifier
                        _sb.Length = 0;
                        while (Char.IsLetterOrDigit(_currentChar) || _currentChar == '_' || _currentChar == '$')
                        {
                            _sb.Append(_currentChar);
                            NextChar();
                        }
                        String = _sb.ToString();

                        // Handle special identifiers
                        switch (String)
                        {
                            case "true":
                                LiteralKind = LiteralKind.True;
                                CurrentToken = Token.Literal;
                                return;

                            case "false":
                                LiteralKind = LiteralKind.False;
                                CurrentToken = Token.Literal;
                                return;

                            case "null":
                                LiteralKind = LiteralKind.Null;
                                CurrentToken = Token.Literal;
                                return;
                        }

                        CurrentToken = Token.Identifier;
                        return;
                    }

                    // What the?
                    throw new InvalidDataException(string.Format("syntax error, unexpected character '{0}'", _currentChar));
                }
            }

            // Parse a sequence of characters that could make up a valid number
            // For performance, we don't actually parse it into a number yet.  When using PetaJsonEmit we parse
            // later, directly into a value type to avoid boxing
            private void TokenizeNumber()
            {
                _sb.Length = 0;

                // Leading negative sign
                bool signed = false;
                if (_currentChar == '-')
                {
                    signed = true;
                    _sb.Append(_currentChar);
                    NextChar();
                }

                // Hex prefix?
                bool hex = false;
                if (_currentChar == '0' && (_options & JsonOptions.StrictParser) == 0)
                {
                    _sb.Append(_currentChar);
                    NextChar();
                    if (_currentChar == 'x' || _currentChar == 'X')
                    {
                        _sb.Append(_currentChar);
                        NextChar();
                        hex = true;
                    }
                }

                // Process characters, but vaguely figure out what type it is
                bool cont = true;
                bool fp = false;
                while (cont)
                {
                    switch (_currentChar)
                    {
                        case '0':
                        case '1':
                        case '2':
                        case '3':
                        case '4':
                        case '5':
                        case '6':
                        case '7':
                        case '8':
                        case '9':
                            _sb.Append(_currentChar);
                            NextChar();
                            break;

                        case 'A':
                        case 'a':
                        case 'B':
                        case 'b':
                        case 'C':
                        case 'c':
                        case 'D':
                        case 'd':
                        case 'F':
                        case 'f':
                            if (!hex)
                                cont = false;
                            else
                            {
                                _sb.Append(_currentChar);
                                NextChar();
                            }
                            break;

                        case '.':
                            if (hex)
                            {
                                cont = false;
                            }
                            else
                            {
                                fp = true;
                                _sb.Append(_currentChar);
                                NextChar();
                            }
                            break;

                        case 'E':
                        case 'e':
                            if (!hex)
                            {
                                fp = true;
                                _sb.Append(_currentChar);
                                NextChar();
                                if (_currentChar == '+' || _currentChar == '-')
                                {
                                    _sb.Append(_currentChar);
                                    NextChar();
                                }
                            }
                            break;

                        default:
                            cont = false;
                            break;
                    }
                }

                if (char.IsLetter(_currentChar))
                    throw new InvalidDataException(string.Format("syntax error, invalid character following number '{0}'", _sb.ToString()));

                // Setup token
                String = _sb.ToString();
                CurrentToken = Token.Literal;

                // Setup literal kind
                if (fp)
                {
                    LiteralKind = LiteralKind.FloatingPoint;
                }
                else if (signed)
                {
                    LiteralKind = LiteralKind.SignedInteger;
                }
                else
                {
                    LiteralKind = LiteralKind.UnsignedInteger;
                }
            }

            // Check the current token, throw exception if mismatch
            public void Check(Token tokenRequired)
            {
                if (tokenRequired != CurrentToken)
                {
                    throw new InvalidDataException(string.Format("syntax error, expected {0} found {1}", tokenRequired, CurrentToken));
                }
            }

            // Skip token which must match
            public void Skip(Token tokenRequired)
            {
                Check(tokenRequired);
                NextToken();
            }

            // Skip token if it matches
            public bool SkipIf(Token tokenRequired)
            {
                if (tokenRequired == CurrentToken)
                {
                    NextToken();
                    return true;
                }
                return false;
            }
        }

#if !PETAJSON_NO_EMIT


        static class Emit
        {

            private static bool TypeArrayContains(System.Type[] types, System.Type type)
            {
                for (int i = 0; i < types.Length; ++i)
                {
                    if (types[i] == type)
                        return true;
                }

                return false;
            }


            // Generates a function that when passed an object of specified type, renders it to an IJsonReader
            public static WriteCallback_t<IJsonWriter, object> MakeFormatter(Type type)
            {
                System.Reflection.MethodInfo formatJson = ReflectionInfo.FindFormatJson(type);
                if (formatJson != null)
                {
                    System.Reflection.Emit.DynamicMethod method = new DynamicMethod("invoke_formatJson", null, new Type[] { typeof(IJsonWriter), typeof(Object) }, true);
                    System.Reflection.Emit.ILGenerator il = method.GetILGenerator();
                    if (formatJson.ReturnType == typeof(string))
                    {
                        // w.WriteStringLiteral(o.FormatJson())
                        il.Emit(OpCodes.Ldarg_0); 
                        il.Emit(OpCodes.Ldarg_1);
                        il.Emit(OpCodes.Unbox, type);
                        il.Emit(OpCodes.Call, formatJson);
                        il.Emit(OpCodes.Callvirt, typeof(IJsonWriter).GetMethod("WriteStringLiteral"));
                    }
                    else
                    {
                        // o.FormatJson(w);
                        il.Emit(OpCodes.Ldarg_1);
                        il.Emit(type.IsValueType ? OpCodes.Unbox : OpCodes.Castclass, type);
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(type.IsValueType ? OpCodes.Call : OpCodes.Callvirt, formatJson);
                    }
                    il.Emit(OpCodes.Ret);
                    return (WriteCallback_t<IJsonWriter, object>)method.CreateDelegate(typeof(WriteCallback_t<IJsonWriter, object>));
                }
                else
                {
                    // Get the reflection info for this type
                    ReflectionInfo ri = ReflectionInfo.GetReflectionInfo(type);
                    if (ri == null)
                        return null;

                    // Create a dynamic method that can do the work
                    DynamicMethod method = new DynamicMethod("dynamic_formatter", null, new Type[] { typeof(IJsonWriter), typeof(object) }, true);
                    System.Reflection.Emit.ILGenerator il = method.GetILGenerator();

                    // Cast/unbox the target object and store in local variable
                    System.Reflection.Emit.LocalBuilder locTypedObj = il.DeclareLocal(type);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(type.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, type);
                    il.Emit(OpCodes.Stloc, locTypedObj);

                    // Get Invariant CultureInfo (since we'll probably be needing this)
                    System.Reflection.Emit.LocalBuilder locInvariant = il.DeclareLocal(typeof(IFormatProvider));
                    il.Emit(OpCodes.Call, typeof(CultureInfo).GetProperty("InvariantCulture").GetGetMethod());
                    il.Emit(OpCodes.Stloc, locInvariant);

                    // These are the types we'll call .ToString(Culture.InvariantCulture) on
                    System.Type[] toStringTypes = new Type[] { 
                        typeof(int), typeof(uint), typeof(long), typeof(ulong), 
                        typeof(short), typeof(ushort), typeof(decimal), 
                        typeof(byte), typeof(sbyte)
                    };

                    // Theses types we also generate for
                    System.Type[] otherSupportedTypes = new Type[] {
                        typeof(double), typeof(float), typeof(string), typeof(char)
                    };

                    // Call IJsonWriting if implemented
                    if (typeof(IJsonWriting).IsAssignableFrom(type))
                    {
                        if (type.IsValueType)
                        {
                            il.Emit(OpCodes.Ldloca, locTypedObj);
                            il.Emit(OpCodes.Ldarg_0);
                            il.Emit(OpCodes.Call, type.GetInterfaceMap(typeof(IJsonWriting)).TargetMethods[0]);
                        }
                        else
                        {
                            il.Emit(OpCodes.Ldloc, locTypedObj);
                            il.Emit(OpCodes.Castclass, typeof(IJsonWriting));
                            il.Emit(OpCodes.Ldarg_0);
                            il.Emit(OpCodes.Callvirt, typeof(IJsonWriting).GetMethod("OnJsonWriting", new Type[] { typeof(IJsonWriter) }));
                        }
                    }

                    // Process all members
                    foreach (JsonMemberInfo m in ri.Members)
                    {
                        // Dont save deprecated properties
                        if (m.Deprecated)
                        {
                            continue;
                        }

                        // Ignore write only properties
                        PropertyInfo pi = m.Member as PropertyInfo;
                        if (pi != null && pi.GetGetMethod(true) == null)
                        {
                            continue;
                        }

                        // Write the Json key
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldstr, m.JsonKey);
                        il.Emit(OpCodes.Callvirt, typeof(IJsonWriter).GetMethod("WriteKeyNoEscaping", new Type[] { typeof(string) }));

                        // Load the writer
                        il.Emit(OpCodes.Ldarg_0);

                        // Get the member type
                        System.Type memberType = m.MemberType;

                        // Load the target object
                        if (type.IsValueType)
                        {
                            il.Emit(OpCodes.Ldloca, locTypedObj);
                        }
                        else
                        {
                            il.Emit(OpCodes.Ldloc, locTypedObj);
                        }

                        // Work out if we need the value or it's address on the stack
                        bool NeedValueAddress = (memberType.IsValueType && (TypeArrayContains(toStringTypes, memberType) || TypeArrayContains(otherSupportedTypes, memberType)));
                        if (Nullable.GetUnderlyingType(memberType) != null)
                        {
                            NeedValueAddress = true;
                        }

                        // Property?
                        if (pi != null)
                        {
                            // Call property's get method
                            if (type.IsValueType)
                                il.Emit(OpCodes.Call, pi.GetGetMethod(true));
                            else
                                il.Emit(OpCodes.Callvirt, pi.GetGetMethod(true));

                            // If we need the address then store in a local and take it's address
                            if (NeedValueAddress)
                            {
                                System.Reflection.Emit.LocalBuilder locTemp = il.DeclareLocal(memberType);
                                il.Emit(OpCodes.Stloc, locTemp);
                                il.Emit(OpCodes.Ldloca, locTemp);
                            }
                        }

                        // Field?
                        FieldInfo fi = m.Member as FieldInfo;
                        if (fi != null)
                        {
                            if (NeedValueAddress)
                            {
                                il.Emit(OpCodes.Ldflda, fi);
                            }
                            else
                            {
                                il.Emit(OpCodes.Ldfld, fi);
                            }
                        }

                        Label? lblFinished = null;

                        // Is it a nullable type?
                        System.Type typeUnderlying = Nullable.GetUnderlyingType(memberType);
                        if (typeUnderlying != null)
                        {
                            // Duplicate the address so we can call get_HasValue() and then get_Value()
                            il.Emit(OpCodes.Dup);

                            // Define some labels
                            System.Reflection.Emit.Label lblHasValue = il.DefineLabel();
                            lblFinished = il.DefineLabel();

                            // Call has_Value
                            il.Emit(OpCodes.Call, memberType.GetProperty("HasValue").GetGetMethod());
                            il.Emit(OpCodes.Brtrue, lblHasValue);

                            // No value, write "null:
                            il.Emit(OpCodes.Pop);
                            il.Emit(OpCodes.Ldstr, "null");
                            il.Emit(OpCodes.Callvirt, typeof(IJsonWriter).GetMethod("WriteRaw", new Type[] { typeof(string) }));
                            il.Emit(OpCodes.Br_S, lblFinished.Value);

                            // Get it's value
                            il.MarkLabel(lblHasValue);
                            il.Emit(OpCodes.Call, memberType.GetProperty("Value").GetGetMethod());

                            // Switch to the underlying type from here on
                            memberType = typeUnderlying;
                            NeedValueAddress = (memberType.IsValueType && (TypeArrayContains(toStringTypes, memberType) || TypeArrayContains(otherSupportedTypes, memberType)));

                            // Work out again if we need the address of the value
                            if (NeedValueAddress)
                            {
                                System.Reflection.Emit.LocalBuilder locTemp = il.DeclareLocal(memberType);
                                il.Emit(OpCodes.Stloc, locTemp);
                                il.Emit(OpCodes.Ldloca, locTemp);
                            }
                        }

                        // ToString()
                        if (TypeArrayContains(toStringTypes, memberType))
                        {
                            // Convert to string
                            il.Emit(OpCodes.Ldloc, locInvariant);
                            il.Emit(OpCodes.Call, memberType.GetMethod("ToString", new Type[] { typeof(IFormatProvider) }));
                            il.Emit(OpCodes.Callvirt, typeof(IJsonWriter).GetMethod("WriteRaw", new Type[] { typeof(string) }));
                        }

                        // ToString("R")
                        else if (memberType == typeof(float) || memberType == typeof(double))
                        {
                            il.Emit(OpCodes.Ldstr, "R");
                            il.Emit(OpCodes.Ldloc, locInvariant);
                            il.Emit(OpCodes.Call, memberType.GetMethod("ToString", new Type[] { typeof(string), typeof(IFormatProvider) }));
                            il.Emit(OpCodes.Callvirt, typeof(IJsonWriter).GetMethod("WriteRaw", new Type[] { typeof(string) }));
                        }

                        // String?
                        else if (memberType == typeof(string))
                        {
                            il.Emit(OpCodes.Callvirt, typeof(IJsonWriter).GetMethod("WriteStringLiteral", new Type[] { typeof(string) }));
                        }

                        // Char?
                        else if (memberType == typeof(char))
                        {
                            il.Emit(OpCodes.Call, memberType.GetMethod("ToString", new Type[] { }));
                            il.Emit(OpCodes.Callvirt, typeof(IJsonWriter).GetMethod("WriteStringLiteral", new Type[] { typeof(string) }));
                        }

                        // Bool?
                        else if (memberType == typeof(bool))
                        {
                            System.Reflection.Emit.Label lblTrue = il.DefineLabel();
                            System.Reflection.Emit.Label lblCont = il.DefineLabel();
                            il.Emit(OpCodes.Brtrue_S, lblTrue);
                            il.Emit(OpCodes.Ldstr, "false");
                            il.Emit(OpCodes.Br_S, lblCont);
                            il.MarkLabel(lblTrue);
                            il.Emit(OpCodes.Ldstr, "true");
                            il.MarkLabel(lblCont);
                            il.Emit(OpCodes.Callvirt, typeof(IJsonWriter).GetMethod("WriteRaw", new Type[] { typeof(string) }));
                        }

                        // NB: We don't support DateTime as it's format can be changed

                        else
                        {
                            // Unsupported type, pass through
                            if (memberType.IsValueType)
                            {
                                il.Emit(OpCodes.Box, memberType);
                            }
                            il.Emit(OpCodes.Callvirt, typeof(IJsonWriter).GetMethod("WriteValue", new Type[] { typeof(object) }));
                        }

                        if (lblFinished.HasValue)
                            il.MarkLabel(lblFinished.Value);
                    }

                    // Call IJsonWritten
                    if (typeof(IJsonWritten).IsAssignableFrom(type))
                    {
                        if (type.IsValueType)
                        {
                            il.Emit(OpCodes.Ldloca, locTypedObj);
                            il.Emit(OpCodes.Ldarg_0);
                            il.Emit(OpCodes.Call, type.GetInterfaceMap(typeof(IJsonWritten)).TargetMethods[0]);
                        }
                        else
                        {
                            il.Emit(OpCodes.Ldloc, locTypedObj);
                            il.Emit(OpCodes.Castclass, typeof(IJsonWriting));
                            il.Emit(OpCodes.Ldarg_0);
                            il.Emit(OpCodes.Callvirt, typeof(IJsonWriting).GetMethod("OnJsonWritten", new Type[] { typeof(IJsonWriter) }));
                        }
                    }

                    // Done!
                    il.Emit(OpCodes.Ret);
                    WriteCallback_t<IJsonWriter, object> impl = (WriteCallback_t<IJsonWriter, object>)method.CreateDelegate(typeof(WriteCallback_t<IJsonWriter, object>));

                    // Wrap it in a call to WriteDictionary
                    return (w, obj) =>
                    {
                        w.WriteDictionary(() =>
                            {
                                impl(w, obj);
                            });
                    };
                }
            }

            // Pseudo box lets us pass a value type by reference.  Used during 
            // deserialization of value types.
            interface IPseudoBox
            {
                object GetValue();
            }
            
            class PseudoBox<T> : IPseudoBox where T : struct
            {
                public T value = default(T);
                object IPseudoBox.GetValue() { return value; }
            }


            // Make a parser for value types
            public static ReadCallback_t<IJsonReader, Type, object> MakeParser(Type type)
            {
                System.Diagnostics.Debug.Assert(type.IsValueType);

                // ParseJson method?
                System.Reflection.MethodInfo parseJson = ReflectionInfo.FindParseJson(type);
                if (parseJson != null)
                {
                    if (parseJson.GetParameters()[0].ParameterType == typeof(IJsonReader))
                    {
                        DynamicMethod method = new DynamicMethod("invoke_ParseJson", typeof(Object), new Type[] { typeof(IJsonReader), typeof(Type) }, true);
                        System.Reflection.Emit.ILGenerator il = method.GetILGenerator();

                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Call, parseJson);
                        il.Emit(OpCodes.Box, type);
                        il.Emit(OpCodes.Ret);
                        return (ReadCallback_t<IJsonReader,Type,object>)method.CreateDelegate(typeof(ReadCallback_t<IJsonReader,Type,object>));
                    }
                    else
                    {
                        DynamicMethod method = new DynamicMethod("invoke_ParseJson", typeof(Object), new Type[] { typeof(string) }, true);
                        System.Reflection.Emit.ILGenerator il = method.GetILGenerator();

                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Call, parseJson);
                        il.Emit(OpCodes.Box, type);
                        il.Emit(OpCodes.Ret);
                        ReadCallback_t<string, object> invoke = (ReadCallback_t<string, object>)method.CreateDelegate(typeof(ReadCallback_t<string, object>));

                        return (r, t) =>
                        {
                            if (r.GetLiteralKind() == LiteralKind.String)
                            {
                                object o = invoke(r.GetLiteralString());
                                r.NextToken();
                                return o;
                            }
                            throw new InvalidDataException(string.Format("Expected string literal for type {0}", type.FullName));
                        };
                    }
                }
                else
                {
                    // Get the reflection info for this type
                    ReflectionInfo ri = ReflectionInfo.GetReflectionInfo(type);
                    if (ri == null)
                        return null;

                    // We'll create setters for each property/field
                    Dictionary<string, WriteCallback_t<IJsonReader, object>> setters = new Dictionary<string, WriteCallback_t<IJsonReader, object>>();

                    // Store the value in a pseudo box until it's fully initialized
                    System.Type boxType = typeof(PseudoBox<>).MakeGenericType(type);

                    // Process all members
                    foreach (JsonMemberInfo m in ri.Members)
                    {
                        // Ignore write only properties
                        PropertyInfo pi = m.Member as PropertyInfo;
                        FieldInfo fi = m.Member as FieldInfo;
                        if (pi != null && pi.GetSetMethod(true) == null)
                        {
                            continue;
                        }

                        // Create a dynamic method that can do the work
                        DynamicMethod method = new DynamicMethod("dynamic_parser", null, new Type[] { typeof(IJsonReader), typeof(object) }, true);
                        System.Reflection.Emit.ILGenerator il = method.GetILGenerator();

                        // Load the target
                        il.Emit(OpCodes.Ldarg_1);
                        il.Emit(OpCodes.Castclass, boxType);
                        il.Emit(OpCodes.Ldflda, boxType.GetField("value"));

                        // Get the value
                        GenerateGetJsonValue(m, il);

                        // Assign it
                        if (pi != null)
                            il.Emit(OpCodes.Call, pi.GetSetMethod(true));
                        if (fi != null)
                            il.Emit(OpCodes.Stfld, fi);

                        // Done
                        il.Emit(OpCodes.Ret);

                        // Store in the map of setters
                        setters.Add(m.JsonKey, (WriteCallback_t<IJsonReader, object>)method.CreateDelegate(typeof(WriteCallback_t<IJsonReader, object>)));
                    }

                    // Create helpers to invoke the interfaces (this is painful but avoids having to really box 
                    // the value in order to call the interface).
                    WriteCallback_t<object, IJsonReader> invokeLoading = MakeInterfaceCall(type, typeof(IJsonLoading));
                    WriteCallback_t<object, IJsonReader> invokeLoaded = MakeInterfaceCall(type, typeof(IJsonLoaded));
                    ReadCallback_t<object, IJsonReader, string, bool> invokeField = MakeLoadFieldCall(type);

                    // Create the parser
                    ReadCallback_t<IJsonReader, Type, object> parser = (reader, Type) =>
                        {
                            // Create pseudobox (ie: new PseudoBox<Type>)
                            object box = DecoratingActivator.CreateInstance(boxType);

                            // Call IJsonLoading
                            if (invokeLoading != null)
                                invokeLoading(box, reader);

                            // Read the dictionary
                            reader.ParseDictionary(key =>
                                {
                                    // Call IJsonLoadField
                                    if (invokeField != null && invokeField(box, reader, key))
                                        return;

                                    // Get a setter and invoke it if found
                                    WriteCallback_t<IJsonReader, object> setter;
                                    if (setters.TryGetValue(key, out setter))
                                    {
                                        setter(reader, box);
                                    }
                                });

                            // IJsonLoaded
                            if (invokeLoaded != null)
                                invokeLoaded(box, reader);

                            // Return the value
                            return ((IPseudoBox)box).GetValue();
                        };

                    // Done
                    return parser;
                }
            }

            // Helper to make the call to a PsuedoBox value's IJsonLoading or IJsonLoaded
            static WriteCallback_t<object, IJsonReader> MakeInterfaceCall(Type type, Type tItf)
            {
                // Interface supported?
                if (!tItf.IsAssignableFrom(type))
                    return null;

                // Resolve the box type
                System.Type boxType = typeof(PseudoBox<>).MakeGenericType(type);

                // Create method
                DynamicMethod method = new DynamicMethod("dynamic_invoke_" + tItf.Name, null, new Type[] { typeof(object), typeof(IJsonReader) }, true);
                System.Reflection.Emit.ILGenerator il = method.GetILGenerator();

                // Call interface method
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, boxType);
                il.Emit(OpCodes.Ldflda, boxType.GetField("value"));
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Call, type.GetInterfaceMap(tItf).TargetMethods[0]);
                il.Emit(OpCodes.Ret);

                // Done
                return (WriteCallback_t<object, IJsonReader>)method.CreateDelegate(typeof(WriteCallback_t<object, IJsonReader>));
            }

            // Similar to above but for IJsonLoadField
            static ReadCallback_t<object, IJsonReader, string, bool> MakeLoadFieldCall(Type type)
            {
                // Interface supported?
                System.Type tItf = typeof(IJsonLoadField);
                if (!tItf.IsAssignableFrom(type))
                    return null;

                // Resolve the box type
                System.Type boxType = typeof(PseudoBox<>).MakeGenericType(type);

                // Create method
                DynamicMethod method = new DynamicMethod("dynamic_invoke_" + tItf.Name, typeof(bool), new Type[] { typeof(object), typeof(IJsonReader), typeof(string) }, true);
                System.Reflection.Emit.ILGenerator il = method.GetILGenerator();

                // Call interface method
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, boxType);
                il.Emit(OpCodes.Ldflda, boxType.GetField("value"));
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Call, type.GetInterfaceMap(tItf).TargetMethods[0]);
                il.Emit(OpCodes.Ret);

                // Done
                return (ReadCallback_t<object, IJsonReader, string, bool>)method.CreateDelegate(typeof(ReadCallback_t<object, IJsonReader, string, bool>));
            }

            // Create an "into parser" that can parse from IJsonReader into a reference type (ie: a class)
            public static WriteCallback_t<IJsonReader, object> MakeIntoParser(Type type)
            {
                System.Diagnostics.Debug.Assert(!type.IsValueType);

                // Get the reflection info for this type
                ReflectionInfo ri = ReflectionInfo.GetReflectionInfo(type);
                if (ri == null)
                    return null;

                // We'll create setters for each property/field
                Dictionary<string, WriteCallback_t<IJsonReader, object>> setters = new Dictionary<string, WriteCallback_t<IJsonReader, object>>();

                // Process all members
                foreach (JsonMemberInfo m in ri.Members)
                {
                    // Ignore write only properties
                    PropertyInfo pi = m.Member as PropertyInfo;
                    FieldInfo fi = m.Member as FieldInfo;
                    if (pi != null && pi.GetSetMethod(true) == null)
                    {
                        continue;
                    }

                    // Ignore read only properties that has KeepInstance attribute
                    if (pi != null && pi.GetGetMethod(true) == null && m.KeepInstance)
                    {
                        continue;
                    }

                    // Create a dynamic method that can do the work
                    DynamicMethod method = new DynamicMethod("dynamic_parser", null, new Type[] { typeof(IJsonReader), typeof(object) }, true);
                    System.Reflection.Emit.ILGenerator il = method.GetILGenerator();

                    // Load the target
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Castclass, type);

                    // Try to keep existing instance?
                    if (m.KeepInstance)
                    {
                        // Get existing existing instance
                        il.Emit(OpCodes.Dup);
                        if (pi != null)
                            il.Emit(OpCodes.Callvirt, pi.GetGetMethod(true));
                        else
                            il.Emit(OpCodes.Ldfld, fi);

                        System.Reflection.Emit.LocalBuilder existingInstance = il.DeclareLocal(m.MemberType);
                        System.Reflection.Emit.Label lblExistingInstanceNull = il.DefineLabel();

                        // Keep a copy of the existing instance in a locale
                        il.Emit(OpCodes.Dup);
                        il.Emit(OpCodes.Stloc, existingInstance);

                        // Compare to null
                        il.Emit(OpCodes.Ldnull);
                        il.Emit(OpCodes.Ceq);
                        il.Emit(OpCodes.Brtrue_S, lblExistingInstanceNull);

                        il.Emit(OpCodes.Ldarg_0);                       // reader
                        il.Emit(OpCodes.Ldloc, existingInstance);       // into
                        il.Emit(OpCodes.Callvirt, typeof(IJsonReader).GetMethod("ParseInto", new Type[] { typeof(Object) }));

                        il.Emit(OpCodes.Pop);       // Clean up target left on stack (1)
                        il.Emit(OpCodes.Ret);

                        il.MarkLabel(lblExistingInstanceNull);
                    }

                    // Get the value from IJsonReader
                    GenerateGetJsonValue(m, il);

                    // Assign it
                    if (pi != null)
                        il.Emit(OpCodes.Callvirt, pi.GetSetMethod(true));
                    if (fi != null)
                        il.Emit(OpCodes.Stfld, fi);

                    // Done
                    il.Emit(OpCodes.Ret);

                    // Store the handler in map
                    setters.Add(m.JsonKey, (WriteCallback_t<IJsonReader, object>)method.CreateDelegate(typeof(WriteCallback_t<IJsonReader, object>)));
                }


                // Now create the parseInto delegate
                WriteCallback_t<IJsonReader, object> parseInto = (reader, obj) =>
                    {
                        // Call IJsonLoading
                        IJsonLoading loading = obj as IJsonLoading;
                        if (loading != null)
                            loading.OnJsonLoading(reader);

                        // Cache IJsonLoadField
                        IJsonLoadField lf = obj as IJsonLoadField;

                        // Read dictionary keys
                        reader.ParseDictionary(key =>
                            {
                                // Call IJsonLoadField
                                if (lf != null && lf.OnJsonField(reader, key))
                                    return;

                                // Call setters
                                WriteCallback_t<IJsonReader, object> setter;
                                if (setters.TryGetValue(key, out setter))
                                {
                                    setter(reader, obj);
                                }
                            });

                        // Call IJsonLoaded
                        IJsonLoaded loaded = obj as IJsonLoaded;
                        if (loaded != null)
                            loaded.OnJsonLoaded(reader);
                    };

                // Since we've created the ParseInto handler, we might as well register
                // as a Parse handler too.
                RegisterIntoParser(type, parseInto);

                // Done
                return parseInto;
            }

            // Registers a ParseInto handler as Parse handler that instantiates the object
            // and then parses into it.
            static void RegisterIntoParser(Type type, WriteCallback_t<IJsonReader, object> parseInto)
            {
                // Check type has a parameterless constructor
                System.Reflection.ConstructorInfo con = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[0], null);
                if (con == null)
                    return;

                // Create a dynamic method that can do the work
                DynamicMethod method = new DynamicMethod("dynamic_factory", typeof(object), new Type[] { typeof(IJsonReader), typeof(WriteCallback_t<IJsonReader, object>) }, true);
                System.Reflection.Emit.ILGenerator il = method.GetILGenerator();

                // Create the new object
                System.Reflection.Emit.LocalBuilder locObj = il.DeclareLocal(typeof(object));
                il.Emit(OpCodes.Newobj, con);

                il.Emit(OpCodes.Dup);               // For return value

                il.Emit(OpCodes.Stloc, locObj);

                il.Emit(OpCodes.Ldarg_1);           // parseinto delegate
                il.Emit(OpCodes.Ldarg_0);           // IJsonReader
                il.Emit(OpCodes.Ldloc, locObj);     // new object instance
                il.Emit(OpCodes.Callvirt, typeof(WriteCallback_t<IJsonReader, object>).GetMethod("Invoke"));
                il.Emit(OpCodes.Ret);

                ReadCallback_t<IJsonReader, WriteCallback_t<IJsonReader, object>, object> factory = (ReadCallback_t<IJsonReader, WriteCallback_t<IJsonReader, object>, object>)method.CreateDelegate(typeof(ReadCallback_t<IJsonReader, WriteCallback_t<IJsonReader, object>, object>));

                Json.RegisterParser(type, (reader, type2) =>
                    {
                        return factory(reader, parseInto);
                    });
            }

            // Generate the MSIL to retrieve a value for a particular field or property from a IJsonReader
            private static void GenerateGetJsonValue(JsonMemberInfo m, ILGenerator il)
            {
                WriteCallback_t<string> generateCallToHelper = helperName =>
                    {
                        // Call the helper
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Call, typeof(Emit).GetMethod(helperName, new Type[] { typeof(IJsonReader) }));

                        // Move to next token
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Callvirt, typeof(IJsonReader).GetMethod("NextToken", new Type[] { }));
                    };

                Type[] numericTypes = new Type[] { 
                    typeof(int), typeof(uint), typeof(long), typeof(ulong), 
                    typeof(short), typeof(ushort), typeof(decimal), 
                    typeof(byte), typeof(sbyte), 
                    typeof(double), typeof(float)
                };

                if (m.MemberType == typeof(string))
                {
                    generateCallToHelper("GetLiteralString");
                }

                else if (m.MemberType == typeof(bool))
                {
                    generateCallToHelper("GetLiteralBool");
                }

                else if (m.MemberType == typeof(char))
                {
                    generateCallToHelper("GetLiteralChar");
                }

                else if (TypeArrayContains(numericTypes, m.MemberType))
                {
                    // Get raw number string
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, typeof(Emit).GetMethod("GetLiteralNumber", new Type[] { typeof(IJsonReader) }));

                    // Convert to a string
                    il.Emit(OpCodes.Call, typeof(CultureInfo).GetProperty("InvariantCulture").GetGetMethod());
                    il.Emit(OpCodes.Call, m.MemberType.GetMethod("Parse", new Type[] { typeof(string), typeof(IFormatProvider) }));

                    // 
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Callvirt, typeof(IJsonReader).GetMethod("NextToken", new Type[] { }));
                }

                else
                {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldtoken, m.MemberType);
                    il.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle", new Type[] { typeof(RuntimeTypeHandle) }));
                    il.Emit(OpCodes.Callvirt, typeof(IJsonReader).GetMethod("Parse", new Type[] { typeof(Type) }));
                    il.Emit(m.MemberType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, m.MemberType);
                }
            }

            // Helper to fetch a literal bool from an IJsonReader
            public static bool GetLiteralBool(IJsonReader r)
            {
                switch (r.GetLiteralKind())
                {
                    case LiteralKind.True:
                        return true;

                    case LiteralKind.False:
                        return false;

                    default:
                        throw new InvalidDataException("expected a boolean value");
                }
            }
            
            // Helper to fetch a literal character from an IJsonReader
            public static char GetLiteralChar(IJsonReader r)
            {
                if (r.GetLiteralKind() != LiteralKind.String)
                    throw new InvalidDataException("expected a single character string literal");
                string str = r.GetLiteralString();
                if (str == null || str.Length != 1)
                    throw new InvalidDataException("expected a single character string literal");

                return str[0];
            }

            // Helper to fetch a literal string from an IJsonReader
            public static string GetLiteralString(IJsonReader r)
            {
                switch (r.GetLiteralKind())
                {
                    case LiteralKind.Null: return null;
                    case LiteralKind.String: return r.GetLiteralString();
                }
                throw new InvalidDataException("expected a string literal");
            }
            
            
            // Helper to fetch a literal number from an IJsonReader (returns the raw string)
            public static string GetLiteralNumber(IJsonReader r)
            {
                switch (r.GetLiteralKind())
                {
                    case LiteralKind.SignedInteger:
                    case LiteralKind.UnsignedInteger:
                    case LiteralKind.FloatingPoint:
                        return r.GetLiteralString();
                }
                throw new InvalidDataException("expected a numeric literal");
            }
        }
#endif
    }
}
