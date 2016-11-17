﻿using System;
using System.IO;
using Newtonsoft.Json;

namespace Infrastructure
{
	public class JsonLoader<T> : Singleton<JsonLoader<T>> where T : class, new()
	{
		private String _FileName = null;
		public String FileName { 
			set {
				_FileName = value;
			}
		}

		private bool _IsNew;
		public bool IsNew { 
			get {
				Ensure ();
				return _IsNew;
			}
		}

		private bool _Corrupt;
		public bool Corrupt { 
			get {
				Ensure ();
				return _Corrupt;
			}
		}

		private T _Value = null;
		public T Value {
			get {
				Ensure ();
				return _Value; 
			} 
		}

		private void Ensure ()
		{
			if (_Value != null)
			{
				return;
			}

			if (_FileName == null) {
				throw new Exception ("Missing file name for " + GetType ());
			}

			if (File.Exists (_FileName)) {
				try {
					_Value = JsonConvert.DeserializeObject<T> (File.ReadAllText (_FileName));
					_Corrupt = false;
				} catch (Exception e) {
					_Corrupt = true;
				}
			}

			if (_Value == null) {
				_IsNew = true;
				_Value = new T ();
			} else {
				_IsNew = false;
			}
		}

		public void Save() {
			File.WriteAllText (_FileName, JsonConvert.SerializeObject (_Value));
			_Corrupt = false;
			_IsNew = false;
		}

		public void Delete() {
			File.Delete (_FileName);
			_Value = null;
			_Corrupt = false;
			_IsNew = false;
		}
	}
}

