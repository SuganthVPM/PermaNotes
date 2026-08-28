using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using PermaNotes.Core.Models;

class Program {
    static void Main() {
        try {
            string json = File.ReadAllText(@"C:\Users\sugan\AppData\Local\DesktopNotes\notes.json.migrated");
            var opts = new JsonSerializerOptions { WriteIndented = true };
            var list = JsonSerializer.Deserialize<List<Note>>(json, opts);
            Console.WriteLine("Success: " + list.Count);
        } catch (Exception ex) {
            Console.WriteLine("Error: " + ex.ToString());
        }
    }
}
