using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Text;

public class GeminiAdapter : MonoBehaviour
{
    [Header("Configuración Gemini")]
    private string apiKey = Secretos.GEMINI_API_KEY;
    private string apiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

    public Action<string> OnStatusUpdate;

    [Serializable] class GeminiRequest { public Content[] contents; public GenerationConfig generationConfig; }
    [Serializable] class Content { public Part[] parts; }
    [Serializable] class Part { public string text; }
    [Serializable] class GenerationConfig { public string responseMimeType; }
    [Serializable] class GeminiResponse { public Candidate[] candidates; }
    [Serializable] class Candidate { public Content content; }

    public IEnumerator SolicitarAdaptacion(List<string> textosOriginales, string idioma, string modo, Action<List<string>> alTerminar)
    {
        ReportarEstado($"[GEMINI] 1. Iniciando. Líneas: {textosOriginales.Count}");

        if (string.IsNullOrEmpty(apiKey) || apiKey.Contains("TU_API_KEY"))
        {
            ReportarEstado("[GEMINI] ✗ ERROR: No hay API Key");
            alTerminar(null);
            yield break;
        }

        string promptSistema = $"You are a translator. Paraphrase into '{idioma}'. ";
        if (modo == "Adaptacion") promptSistema += "Simplify technical terms to natural language. ";
        
        string jsonInput = ListaAJson(textosOriginales);
        string promptFinal = $"{promptSistema} Return ONLY a raw JSON array of strings. Same length as input. Input: {jsonInput}";

        GeminiRequest requestData = new GeminiRequest();
        requestData.contents = new Content[] { new Content { parts = new Part[] { new Part { text = promptFinal } } } };
        requestData.generationConfig = new GenerationConfig { responseMimeType = "application/json" };

        string jsonBody = JsonUtility.ToJson(requestData);

        Debug.Log($"[GEMINI-SEND] Enviando {textosOriginales.Count} textos");

        using (UnityWebRequest www = new UnityWebRequest($"{apiUrl}?key={apiKey}", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            ReportarEstado("[GEMINI] 2. Enviando a Google...");
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[GEMINI-ERROR] {www.error} (Code: {www.responseCode})");
                Debug.LogError($"[GEMINI-ERROR] Respuesta: {www.downloadHandler.text}");
                ReportarEstado($"Error Gemini: {www.responseCode}");
                alTerminar(null);
            }
            else
            {
                string respuestaRaw = www.downloadHandler.text;
                Debug.Log($"[GEMINI-RECV] OK, procesando...");
                ProcesarRespuestaGemini(respuestaRaw, alTerminar);
            }
        }
    }

    private void ProcesarRespuestaGemini(string jsonRaw, Action<List<string>> callback)
    {
        try
        {
            ReportarEstado("[GEMINI] 3. Procesando respuesta...");
            
            GeminiResponse respuestaObj = JsonUtility.FromJson<GeminiResponse>(jsonRaw);

            if (respuestaObj.candidates == null || respuestaObj.candidates.Length == 0)
            {
                Debug.LogError("[GEMINI] Respuesta vacía");
                callback(null);
                return;
            }

            string textoGenerado = respuestaObj.candidates[0].content.parts[0].text;
            Debug.Log($"[GEMINI-PARSE] Texto generado length: {textoGenerado.Length}");
            
            // Limpiar markdown si existe
            textoGenerado = textoGenerado.Replace("```json", "").Replace("```", "").Trim();
            
            Debug.Log($"[GEMINI-PARSE] JSON limpio: {textoGenerado.Substring(0, Mathf.Min(200, textoGenerado.Length))}...");
            
            List<string> listaFinal = JsonALista(textoGenerado);
            
            Debug.Log($"[GEMINI] ✅ Parseados: {listaFinal.Count} textos");
            ReportarEstado($"[GEMINI] ✅ Éxito. {listaFinal.Count} textos");
            
            callback(listaFinal);
        }
        catch (Exception e)
        {
            Debug.LogError($"[GEMINI] Error parseo: {e.Message}");
            Debug.LogError($"[GEMINI] Stack: {e.StackTrace}");
            callback(null);
        }
    }

    private void ReportarEstado(string mensaje)
    {
        Debug.Log(mensaje);
        OnStatusUpdate?.Invoke(mensaje);
    }

    private List<string> JsonALista(string jsonArray)
    {
        List<string> resultado = new List<string>();
        
        try
        {
            // Limpiar el JSON
            jsonArray = jsonArray.Trim()
                .Replace("\n", "")
                .Replace("\r", "")
                .Replace("  ", " ");
            
            // Quitar corchetes
            if (jsonArray.StartsWith("[")) jsonArray = jsonArray.Substring(1);
            if (jsonArray.EndsWith("]")) jsonArray = jsonArray.Substring(0, jsonArray.Length - 1);
            jsonArray = jsonArray.Trim();
            
            Debug.Log($"[GEMINI-PARSE] JSON para parsear: {jsonArray}");
            
            // Parsear cada string entre comillas
            bool dentroDeComillas = false;
            string textoActual = "";
            
            for (int i = 0; i < jsonArray.Length; i++)
            {
                char c = jsonArray[i];
                
                if (c == '"' && (i == 0 || jsonArray[i - 1] != '\\'))
                {
                    if (dentroDeComillas)
                    {
                        // Fin de string
                        resultado.Add(textoActual);
                        Debug.Log($"[GEMINI-PARSE] #{resultado.Count}: '{textoActual}'");
                        textoActual = "";
                        dentroDeComillas = false;
                    }
                    else
                    {
                        // Inicio de string
                        dentroDeComillas = true;
                    }
                }
                else if (dentroDeComillas)
                {
                    textoActual += c;
                }
            }
            
            Debug.Log($"[GEMINI-PARSE] ✓ Total: {resultado.Count}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GEMINI-PARSE] Error: {e.Message}");
        }
        
        return resultado;
    }

    private string ListaAJson(List<string> lista)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("[");
        for (int i = 0; i < lista.Count; i++)
        {
            sb.Append("\"" + lista[i].Replace("\"", "\\\"") + "\"");
            if (i < lista.Count - 1) sb.Append(", ");
        }
        sb.Append("]");
        return sb.ToString();
    }
}