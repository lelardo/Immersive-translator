using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Text;

public class GeminiAdapter : MonoBehaviour
{
    // ---------------- CONFIGURACIÓN ----------------
    [Header("Configuración Gemini")]
    // ¡OJO! Reemplaza esto con tu clave real de Google AI Studio
    private string apiKey = Secretos.GEMINI_API_KEY;
    private string apiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent";

    // ---------------- ESTRUCTURAS DE DATOS (JSON) ----------------
    // Estas clases sirven para crear el paquete que le enviamos a Gemini
    [Serializable]
    private class GeminiRequest
    {
        public Content[] contents;
    }

    [Serializable]
    private class Content
    {
        public Part[] parts;
    }

    [Serializable]
    private class Part
    {
        public string text;
    }

    // Estas clases sirven para leer lo que Gemini nos responde
    [Serializable]
    private class GeminiResponse
    {
        public Candidate[] candidates;
    }

    [Serializable]
    private class Candidate
    {
        public Content content;
    }

    // Helper para leer listas de strings simples
    [Serializable]
    private class StringListWrapper
    {
        public List<string> items;
    }

    // ---------------- FUNCIÓN PRINCIPAL ----------------
    public IEnumerator SolicitarAdaptacion(List<string> textosOriginales, string idioma, string modo, Action<List<string>> alTerminar)
    {
        // 1. Preparamos el Prompt (La orden para la IA)
        string promptSistema = "";

        if (modo == "Explicación para Niños")
            promptSistema = $"Simplify the following sentences strictly for a 5 year old child in {idioma}. ";
        else if (modo == "Resumen Ejecutivo")
            promptSistema = $"Summarize the main idea of these sentences concisely in {idioma}. ";
        else
            promptSistema = $"Translate these sentences to {idioma} naturally. ";

        // Truco: Convertimos la lista de frases a un texto tipo JSON para que Gemini entienda la estructura
        string jsonInput = ListaAJson(textosOriginales);
        
        string promptFinal = $"{promptSistema}\n\n" +
                             $"INSTRUCTIONS: You will receive a JSON array of strings. You must return ONLY a raw JSON array of strings with the adapted text. " +
                             $"The output array must have exactly the same number of elements as the input. " +
                             $"Format example: [\"text1\", \"text2\"]\n\n" +
                             $"INPUT: {jsonInput}";

        // 2. Empaquetamos los datos para la API
        GeminiRequest requestData = new GeminiRequest();
        requestData.contents = new Content[]
        {
            new Content { parts = new Part[] { new Part { text = promptFinal } } }
        };

        string jsonBody = JsonUtility.ToJson(requestData);

        // 3. Enviamos la petición a Google
        using (UnityWebRequest www = new UnityWebRequest($"{apiUrl}?key={apiKey}", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error Gemini: " + www.error + "\n" + www.downloadHandler.text);
                alTerminar(null); // Avisamos que falló
            }
            else
            {
                // 4. Procesamos la respuesta
                string respuestaRaw = www.downloadHandler.text;
                ProcesarRespuestaGemini(respuestaRaw, alTerminar);
            }
        }
    }

    private void ProcesarRespuestaGemini(string jsonRaw, Action<List<string>> callback)
    {
        try
        {
            // Desempaquetamos la respuesta de Google
            GeminiResponse respuestaObj = JsonUtility.FromJson<GeminiResponse>(jsonRaw);
            string textoGenerado = respuestaObj.candidates[0].content.parts[0].text;

            // Limpieza: A veces Gemini devuelve bloques de código markdown, los quitamos
            textoGenerado = textoGenerado.Replace("```json", "").Replace("```", "").Trim();

            // Convertimos el texto JSON de vuelta a una lista de C#
            // Como JsonUtility es limitado con arrays simples, usamos un truco manual o wrapper
            List<string> listaFinal = JsonALista(textoGenerado);
            
            callback(listaFinal);
        }
        catch (Exception e)
        {
            Debug.LogError("Error leyendo respuesta de Gemini: " + e.Message);
            callback(null);
        }
    }

    // ---- UTILIDADES PARA JSON SIN INSTALAR PLUGINS ----
    
    // Convierte ["hola", "mundo"] (string) -> List<string>
    private List<string> JsonALista(string jsonArray)
    {
        List<string> resultado = new List<string>();
        // Quitamos corchetes
        jsonArray = jsonArray.Replace("[", "").Replace("]", "").Replace("\n", "");
        
        // Separamos por comillas (método rudimentario pero funciona para demos)
        string[] partes = jsonArray.Split(new string[] { "\", \"" }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach(var p in partes)
        {
            string limpio = p.Replace("\"", "").Trim();
            resultado.Add(limpio);
        }
        return resultado;
    }

    // Convierte List<string> -> "[\"hola\", \"mundo\"]"
    private string ListaAJson(List<string> lista)
    {
        string json = "[";
        for (int i = 0; i < lista.Count; i++)
        {
            json += "\"" + lista[i].Replace("\"", "\\\"") + "\""; // Escapar comillas internas
            if (i < lista.Count - 1) json += ", ";
        }
        json += "]";
        return json;
    }
}