
using System.Collections.Generic;

using JsonPair = System.Collections.Generic.KeyValuePair<string, JWT.JSON.JsonValue>;


namespace JWT.JSON
{
	public abstract class JsonValue : System.Collections.IEnumerable
	{
		public static JsonValue Load (System.IO.Stream stream)
		{
			if (stream == null)
				throw new System.ArgumentNullException ("stream");
			return Load (new System.IO.StreamReader (stream, true));
		}

		public static JsonValue Load (System.IO.TextReader textReader)
		{
			if (textReader == null)
				throw new System.ArgumentNullException ("textReader");


            

            object ret = 
                new JavaScriptReader (textReader, true).Read ();

			return ToJsonValue (ret);
		}

		static IEnumerable<KeyValuePair<string,JsonValue>> ToJsonPairEnumerable (IEnumerable<KeyValuePair<string,object>> kvpc)
		{
            foreach (System.Collections.Generic.KeyValuePair<string, object> kvp in kvpc)
				yield return new KeyValuePair<string,JsonValue> (kvp.Key, ToJsonValue (kvp.Value));
		}

		static IEnumerable<JsonValue> ToJsonValueEnumerable (IEnumerable<object> arr)
		{
			foreach (object obj in arr)
				yield return ToJsonValue (obj);
		}

		static JsonValue ToJsonValue (object ret)
		{
			if (ret == null)
				return null;
            IEnumerable<KeyValuePair<string, object>> kvpc = ret as IEnumerable<KeyValuePair<string, object>>;
			if (kvpc != null)
				return new JsonObject (ToJsonPairEnumerable (kvpc));
            IEnumerable<object> arr = ret as IEnumerable<object>;
			if (arr != null)
				return new JsonArray (ToJsonValueEnumerable (arr));

			if (ret is bool)
				return new JsonPrimitive ((bool) ret);
			if (ret is byte)
				return new JsonPrimitive ((byte) ret);
			if (ret is char)
				return new JsonPrimitive ((char) ret);
			if (ret is decimal)
				return new JsonPrimitive ((decimal) ret);
			if (ret is double)
				return new JsonPrimitive ((double) ret);
			if (ret is float)
				return new JsonPrimitive ((float) ret);
			if (ret is int)
				return new JsonPrimitive ((int) ret);
			if (ret is long)
				return new JsonPrimitive ((long) ret);
			if (ret is sbyte)
				return new JsonPrimitive ((sbyte) ret);
			if (ret is short)
				return new JsonPrimitive ((short) ret);
			if (ret is string)
				return new JsonPrimitive ((string) ret);
			if (ret is uint)
				return new JsonPrimitive ((uint) ret);
			if (ret is ulong)
				return new JsonPrimitive ((ulong) ret);
			if (ret is ushort)
				return new JsonPrimitive ((ushort) ret);
			if (ret is System.DateTime)
				return new JsonPrimitive ((System.DateTime) ret);
			if (ret is System.DateTimeOffset)
				return new JsonPrimitive ((System.DateTimeOffset) ret);
			if (ret is System.Guid)
				return new JsonPrimitive ((System.Guid) ret);
			if (ret is System.TimeSpan)
				return new JsonPrimitive ((System.TimeSpan) ret);
			if (ret is System.Uri)
				return new JsonPrimitive ((System.Uri) ret);
			throw new System.NotSupportedException (string.Format ("Unexpected parser return type: {0}", ret.GetType ()));
		}

		public static JsonValue Parse (string jsonString)
		{
			if (jsonString == null)
				throw new System.ArgumentNullException ("jsonString");
			return Load (new System.IO.StringReader (jsonString));
		}

		public virtual int Count {
			get { throw new System.InvalidOperationException (); }
		}

		public abstract JsonType JsonType { get; }

		public virtual JsonValue this [int index] {
			get { throw new System.InvalidOperationException (); }
			set { throw new System.InvalidOperationException (); }
		}

		public virtual JsonValue this [string key] {
			get { throw new System.InvalidOperationException (); }
			set { throw new System.InvalidOperationException (); }
		}

		public virtual bool ContainsKey (string key)
		{
			throw new System.InvalidOperationException ();
		}

		public virtual void Save (System.IO.Stream stream)
		{
			if (stream == null)
				throw new System.ArgumentNullException ("stream");
			Save (new System.IO.StreamWriter (stream));
		}

		public virtual void Save (System.IO.TextWriter textWriter)
		{
			if (textWriter == null)
				throw new System.ArgumentNullException ("textWriter");
			SaveInternal (textWriter);
		}
		
		void SaveInternal (System.IO.TextWriter w)
		{
			switch (JsonType) {
			case JsonType.Object:
				w.Write ('{');
				bool following = false;
				foreach (JsonPair pair in ((JsonObject) this)) {
					if (following)
						w.Write (", ");
					w.Write ('\"');
					w.Write (EscapeString (pair.Key));
					w.Write ("\": ");
					if (pair.Value == null)
						w.Write ("null");
					else
						pair.Value.SaveInternal (w);
					following = true;
				}
				w.Write ('}');
				break;
			case JsonType.Array:
				w.Write ('[');
				following = false;
				foreach (JsonValue v in ((JsonArray) this)) {
					if (following)
						w.Write (", ");
					if (v != null) 
						v.SaveInternal (w);
					else
						w.Write ("null");
					following = true;
				}
				w.Write (']');
				break;
			case JsonType.Boolean:
				w.Write ((bool) this ? "true" : "false");
				break;
			case JsonType.String:
				w.Write ('"');
				w.Write (EscapeString (((JsonPrimitive) this).GetFormattedString ()));
				w.Write ('"');
				break;
			default:
				w.Write (((JsonPrimitive) this).GetFormattedString ());
				break;
			}
		}

		public override string ToString ()
		{
            System.IO.StringWriter sw = new System.IO.StringWriter ();
			Save (sw);
			return sw.ToString ();
		}

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
		{
			throw new System.InvalidOperationException ();
		}

		// Characters which have to be escaped:
		// - Required by JSON Spec: Control characters, '"' and '\\'
		// - Broken surrogates to make sure the JSON string is valid Unicode
		//   (and can be encoded as UTF8)
		// - JSON does not require U+2028 and U+2029 to be escaped, but
		//   JavaScript does require this:
		//   http://stackoverflow.com/questions/2965293/javascript-parse-error-on-u2028-unicode-character/9168133#9168133
		// - '/' also does not have to be escaped, but escaping it when
		//   preceeded by a '<' avoids problems with JSON in HTML <script> tags
		bool NeedEscape (string src, int i) {
			char c = src [i];
			return c < 32 || c == '"' || c == '\\'
				// Broken lead surrogate
				|| (c >= '\uD800' && c <= '\uDBFF' &&
					(i == src.Length - 1 || src [i + 1] < '\uDC00' || src [i + 1] > '\uDFFF'))
				// Broken tail surrogate
				|| (c >= '\uDC00' && c <= '\uDFFF' &&
					(i == 0 || src [i - 1] < '\uD800' || src [i - 1] > '\uDBFF'))
				// To produce valid JavaScript
				|| c == '\u2028' || c == '\u2029'
				// Escape "</" for <script> tags
				|| (c == '/' && i > 0 && src [i - 1] == '<');
		}
		
		internal string EscapeString (string src)
		{
			if (src == null)
				return null;

			for (int i = 0; i < src.Length; i++)
				if (NeedEscape (src, i)) {
                    System.Text.StringBuilder sb = new System.Text.StringBuilder ();
					if (i > 0)
						sb.Append (src, 0, i);
					return DoEscapeString (sb, src, i);
				}
			return src;
		}

		string DoEscapeString (System.Text.StringBuilder sb, string src, int cur)
		{
			int start = cur;
			for (int i = cur; i < src.Length; i++)
				if (NeedEscape (src, i)) {
					sb.Append (src, start, i - start);
					switch (src [i]) {
					case '\b': sb.Append ("\\b"); break;
					case '\f': sb.Append ("\\f"); break;
					case '\n': sb.Append ("\\n"); break;
					case '\r': sb.Append ("\\r"); break;
					case '\t': sb.Append ("\\t"); break;
					case '\"': sb.Append ("\\\""); break;
					case '\\': sb.Append ("\\\\"); break;
					case '/': sb.Append ("\\/"); break;
					default:
						sb.Append ("\\u");
						sb.Append (((int) src [i]).ToString ("x04"));
						break;
					}
					start = i + 1;
				}
			sb.Append (src, start, src.Length - start);
			return sb.ToString ();
		}

		// CLI -> JsonValue

		public static implicit operator JsonValue (bool value)
		{
			return new JsonPrimitive (value);
		}

		public static implicit operator JsonValue (byte value)
		{
			return new JsonPrimitive (value);
		}

		public static implicit operator JsonValue (char value)
		{
			return new JsonPrimitive (value);
		}

		public static implicit operator JsonValue (decimal value)
		{
			return new JsonPrimitive (value);
		}

		public static implicit operator JsonValue (double value)
		{
			return new JsonPrimitive (value);
		}

		public static implicit operator JsonValue (float value)
		{
			return new JsonPrimitive (value);
		}

		public static implicit operator JsonValue (int value)
		{
			return new JsonPrimitive (value);
		}

		public static implicit operator JsonValue (long value)
		{
			return new JsonPrimitive (value);
		}

		public static implicit operator JsonValue (sbyte value)
		{
			return new JsonPrimitive (value);
		}

		public static implicit operator JsonValue (short value)
		{
			return new JsonPrimitive (value);
		}

		public static implicit operator JsonValue (string value)
		{
			return new JsonPrimitive (value);
		}

		public static implicit operator JsonValue (uint value)
		{
			return new JsonPrimitive (value);
		}

		public static implicit operator JsonValue (ulong value)
		{
			return new JsonPrimitive (value);
		}

		public static implicit operator JsonValue (ushort value)
		{
			return new JsonPrimitive (value);
		}

		public static implicit operator JsonValue (System.DateTime value)
		{
			return new JsonPrimitive (value);
		}

		public static implicit operator JsonValue (System.DateTimeOffset value)
		{
			return new JsonPrimitive (value);
		}

		public static implicit operator JsonValue (System.Guid value)
		{
			return new JsonPrimitive (value);
		}

		public static implicit operator JsonValue (System.TimeSpan value)
		{
			return new JsonPrimitive (value);
		}

		public static implicit operator JsonValue (System.Uri value)
		{
			return new JsonPrimitive (value);
		}

		// JsonValue -> CLI

		public static implicit operator bool (JsonValue value)
		{
			if (value == null)
				throw new System.ArgumentNullException ("value");
			return System.Convert.ToBoolean (((JsonPrimitive) value).Value, System.Globalization.NumberFormatInfo.InvariantInfo);
		}

		public static implicit operator byte (JsonValue value)
		{
			if (value == null)
				throw new System.ArgumentNullException ("value");
			return System.Convert.ToByte (((JsonPrimitive) value).Value, System.Globalization.NumberFormatInfo.InvariantInfo);
		}

		public static implicit operator char (JsonValue value)
		{
			if (value == null)
				throw new System.ArgumentNullException ("value");
			return System.Convert.ToChar (((JsonPrimitive) value).Value, System.Globalization.NumberFormatInfo.InvariantInfo);
		}

		public static implicit operator decimal (JsonValue value)
		{
			if (value == null)
				throw new System.ArgumentNullException ("value");
			return System.Convert.ToDecimal (((JsonPrimitive) value).Value, System.Globalization.NumberFormatInfo.InvariantInfo);
		}

		public static implicit operator double (JsonValue value)
		{
			if (value == null)
				throw new System.ArgumentNullException ("value");
			return System.Convert.ToDouble (((JsonPrimitive) value).Value, System.Globalization.NumberFormatInfo.InvariantInfo);
		}

		public static implicit operator float (JsonValue value)
		{
			if (value == null)
				throw new System.ArgumentNullException ("value");
			return System.Convert.ToSingle (((JsonPrimitive) value).Value, System.Globalization.NumberFormatInfo.InvariantInfo);
		}

		public static implicit operator int (JsonValue value)
		{
			if (value == null)
				throw new System.ArgumentNullException ("value");
			return System.Convert.ToInt32 (((JsonPrimitive) value).Value, System.Globalization.NumberFormatInfo.InvariantInfo);
		}

		public static implicit operator long (JsonValue value)
		{
			if (value == null)
				throw new System.ArgumentNullException ("value");
			return System.Convert.ToInt64 (((JsonPrimitive) value).Value, System.Globalization.NumberFormatInfo.InvariantInfo);
		}

		public static implicit operator sbyte (JsonValue value)
		{
			if (value == null)
				throw new System.ArgumentNullException ("value");
			return System.Convert.ToSByte (((JsonPrimitive) value).Value, System.Globalization.NumberFormatInfo.InvariantInfo);
		}

		public static implicit operator short (JsonValue value)
		{
			if (value == null)
				throw new System.ArgumentNullException ("value");
			return System.Convert.ToInt16 (((JsonPrimitive) value).Value, System.Globalization.NumberFormatInfo.InvariantInfo);
		}

		public static implicit operator string (JsonValue value)
		{
			if (value == null)
				return null;
			return (string) ((JsonPrimitive) value).Value;
		}

		public static implicit operator uint (JsonValue value)
		{
			if (value == null)
				throw new System.ArgumentNullException ("value");
			return System.Convert.ToUInt32 (((JsonPrimitive) value).Value, System.Globalization.NumberFormatInfo.InvariantInfo);
		}

		public static implicit operator ulong (JsonValue value)
		{
			if (value == null)
				throw new System.ArgumentNullException ("value");
			return System.Convert.ToUInt64(((JsonPrimitive) value).Value, System.Globalization.NumberFormatInfo.InvariantInfo);
		}

		public static implicit operator ushort (JsonValue value)
		{
			if (value == null)
				throw new System.ArgumentNullException ("value");
			return System.Convert.ToUInt16 (((JsonPrimitive) value).Value, System.Globalization.NumberFormatInfo.InvariantInfo);
		}

		public static implicit operator System.DateTime (JsonValue value)
		{
			if (value == null)
				throw new System.ArgumentNullException ("value");
			return (System.DateTime) ((JsonPrimitive) value).Value;
		}

		public static implicit operator System.DateTimeOffset (JsonValue value)
		{
			if (value == null)
				throw new System.ArgumentNullException ("value");
			return (System.DateTimeOffset) ((JsonPrimitive) value).Value;
		}

		public static implicit operator System.TimeSpan (JsonValue value)
		{
			if (value == null)
				throw new System.ArgumentNullException ("value");
			return (System.TimeSpan) ((JsonPrimitive) value).Value;
		}

		public static implicit operator System.Guid (JsonValue value)
		{
			if (value == null)
				throw new System.ArgumentNullException ("value");
			return (System.Guid) ((JsonPrimitive) value).Value;
		}

		public static implicit operator System.Uri (JsonValue value)
		{
			if (value == null)
				throw new System.ArgumentNullException ("value");
			return (System.Uri) ((JsonPrimitive) value).Value;
		}
	}
}
