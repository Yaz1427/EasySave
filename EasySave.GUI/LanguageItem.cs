using System;
using System.Collections.Generic;
using System.Text;

namespace EasySave.GUI.Models
{
	public class LanguageItem
	{
		public string Code { get; set; } = string.Empty;   // "fr" ou "en"
		public string DisplayName { get; set; } = string.Empty; // "Français" ou "English"
	}
}
