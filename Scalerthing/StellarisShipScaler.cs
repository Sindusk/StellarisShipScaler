using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Metadata.W3cXsd2001;

class Program
{
    public static string TrimValue(string s)
    {
        if (s.Contains("#")) // It contains a comment so we should truncate to just before it.
        {
            return s.Substring(0, s.IndexOf("#")).Trim(); // Trim it to just the value.
        }
        return s.Trim();
    }
    static void Main(string[] args)
    {
        string modDirectory = "C:\\Users\\sinis\\OneDrive\\Documents\\Paradox Interactive\\Stellaris\\mod\\";
        string common = "\\common\\";
        string modName = "! Secrets of the Shroud - Real System Scale 6x Patch";
        string scaleType = "component_templates";
        //string scaleType = "ship_behaviors";
        string directory = modDirectory + modName + common + scaleType + "\\";
        string prefix = "";
        if (scaleType.Equals("ship_behaviors"))
        {
            prefix = "!!!scaled_"; // This is FIOS for ship_behaviors.
        }
        else if (scaleType.Equals("component_templates"))
        {
            prefix = "!!!scaled_"; // This is FIOS for component_templates.
        }
        var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Select(Path.GetFileName);
        FileStream filestream = new FileStream("out.txt", FileMode.Create);
        var streamwriter = new StreamWriter(filestream);
        streamwriter.AutoFlush = true;
        Console.SetOut(streamwriter);
        Console.SetError(streamwriter);
        Dictionary<string, float> variables = new Dictionary<string, float>();
        foreach (string fileName in files)
        {
            Console.WriteLine(" --- Processing file: " + fileName + " ---");
            if (fileName.Contains(prefix)) // Skip files that are already scaled.
            {
                continue;
            }
            StreamReader reader = new StreamReader(directory + fileName);
            string input = "";
            StreamWriter writer = new StreamWriter(directory + prefix + fileName);
            string edited = "";
            string component = ""; // Holds an entire component in memory to write if something changes.
            string[] split;
            bool wasChanged = false;
            string componentName = "";
            while (!reader.EndOfStream)
            {
                input = reader.ReadLine();
                edited = input;
                if (input.StartsWith("@")) // It's a variable, just save to a map and then continue.
                {
                    split = input.Split('=');
                    if(split.Length >= 2)
                    {
                        string key = split[0].Trim(); // The variable name with whitespace trimmed.
                        string strVal = TrimValue(split[1]); // The value of the variable
                        try
                        {
                            float val = float.Parse(strVal);
                            variables[key] =  val; // Add the pair to the dictionary and save the variable.
                            Console.WriteLine(key + ": " + val + " - Variable detected and set");
                        }catch(FormatException)
                        {
                            // Guess it was a string value or something, forget it.
                            Console.WriteLine(key + ": Failed to parse value for variable - " + strVal);
                        }
                    }
                    continue; // Skip it anyway, because it started with @ and we don't care about it.
                }
                if (reader.EndOfStream || (input.Contains("{") && // On a new component, or reached the end of file, attempt to write previous component
                    (input.Contains("weapon_component_template") || // Weapon Components
                    input.Contains("utility_component_template") || // Utility Components
                    input.Contains("ship_behavior")))) // Ship Behaviors
                {
                    if (reader.EndOfStream) // Prevent missing the final bracket if there's no line after.
                    {
                        component = component + input;
                    }
                    if (wasChanged) // The component was changed
                    {
                        if (component.EndsWith("\n"))
                        {
                            component = component.Substring(0, component.Length - 1); // Trim the extra newline if there is one.
                        }
                        writer.WriteLine(component.Substring(0, component.Length)); // Substring to remove the newline at the end.
                        wasChanged = false;
                    }
                    component = ""; // Reset component stored.
                    componentName = ""; // Reset component name.
                }
                if (input.Contains("key") || input.Contains("name")) // This is the name of the current component
                {
                    split = input.Split('=');
                    if (split[0].Trim().Equals("key") || split[0].Trim().Equals("name")) // Found the proper line
                    {
                        if (split.Length >= 2)
                        {
                            componentName = TrimValue(split[1]);
                        }
                    }
                }
                if (input.Contains("range") || input.Contains("distance") || input.Contains("radius"))
                {
                    split = input.Split('=');
                    string trimmed = split[0].Trim();
                    if (!trimmed.Equals("range") && // Weapon or Utility component range
                        !trimmed.Equals("min_range") && // Weapon minimum range
                        !trimmed.Equals("engagement_range") && // Strikecraft engagement range
                        !trimmed.Equals("missile_retarget_range") && // Missile retargeting range
                        !trimmed.Equals("preferred_attack_range") && // Ship behavior preferred attack range
                        !trimmed.Equals("formation_distance") && // Ship behavior preferred formation distance
                        !trimmed.Equals("return_to_formation_distance") && // Ship behavior return to formation distance
                        !trimmed.Equals("radius")) // Hostile Aura Radius
                    {
                        // Do nothing, it's not a proper variable
                        Console.WriteLine(componentName + ": Skipped stat - " + trimmed + ": stat not meant to be scaled.");
                    }
                    else if (split.Length >= 2) // Skip if there's no equal sign setting a value, like a name or something
                    {
                        split[1] = TrimValue(split[1]); // Trims out comments and whitespace.
                        try
                        {
                            float num = float.Parse(split[1]);
                            float newValue = num / 6f;
                            edited = split[0] + "= " + newValue.ToString("n2");
                            wasChanged = true;
                            Console.WriteLine(componentName + ": Scaled stat - " + trimmed + ": " + split[1] + " -> " + newValue.ToString("n2"));
                        }
                        catch (FormatException)
                        {
                            if (split[1].Equals("min") || split[1].Equals("median") || split[1].Equals("max"))
                            {
                                Console.WriteLine(componentName + ": Skipped stat - " + trimmed + ": \"" + split[1] + "\" is a scalar value.");
                            }
                            else if (variables.ContainsKey(split[1])) // Found a variable that matches what's set here.
                            {
                                float num = variables[split[1]]; // Get the value of the variable from the dictionary.
                                float newValue = num / 6f;
                                edited = split[0] + "= " + newValue.ToString("n2");
                                wasChanged = true;
                                Console.WriteLine(componentName + ": Scaled stat - " + trimmed + ": " + num + " (" + split[1] + ") -> " + newValue.ToString("n2"));
                            }
                            else
                            {
                                Console.WriteLine(componentName + ": Error parsing stat - " + trimmed + ": " + split[1]);
                            }
                            // Don't care just continue.
                        }
                    }
                }
                component = component + edited + "\n";
            }
            writer.Close();
            Console.WriteLine(" --- Finished process on file: " + fileName + " ---");
        }
    }
}
