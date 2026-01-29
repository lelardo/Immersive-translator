using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.IO;
using System;

[Serializable]
public class LineaConTraduccion
{
    public string original;
    public string traducido;
    public float posY;
    public float posX;
    public float alto;
    public float ancho;
}

public class TraductorTexto : MonoBehaviour
{
    [Header("Referencias UI")]
    // Borré visorFoto y ajustadorAspecto porque ya no los usas visualmente
    public TextMeshProUGUI textoEstado;
    
    [Header("Configuración")]
    public string idiomaDestino = "en"; 
    
    private string rutaLog;
    private List<LineaConTraduccion> lineas = new List<LineaConTraduccion>();
    
    // IMPORTANTE: Mantenemos esto en memoria aunque no se vea, 
    // porque necesitamos sus dimensiones (ancho/alto) para las matemáticas.
    private Texture2D imagenEnMemoria; 

    [Header("Reconstructor")]
    public ReconstructorDocumento reconstructor;

    void Start()
    {
        rutaLog = Path.Combine(Application.persistentDataPath, "traductor_log.txt");
        textoEstado.text = ""; // Limpio al inicio
    }

    // Este método lo llama tu Gestor de Cámara o OCR
    public void ProcesarOCR(string jsonOCR, Texture2D imagen)
    {
        EscribirLog("=== Datos recibidos del OCR ===");
        
        // Guardamos la referencia solo para saber el tamaño (Width/Height) luego
        imagenEnMemoria = imagen; 
        
        StartCoroutine(ExtraerYTraducir(jsonOCR));
    }

    IEnumerator ExtraerYTraducir(string json)
    {
        textoEstado.text = "Procesando...";
        lineas.Clear();
        
        // --- 1. VALIDACIONES RÁPIDAS ---
        if (json.Contains("\"IsErroredOnProcessing\":true") || string.IsNullOrEmpty(json))
        {
            textoEstado.text = "Error en lectura OCR";
            yield break;
        }

        RespuestaOCR_V2 datos = JsonUtility.FromJson<RespuestaOCR_V2>(json);
        
        if (datos == null || datos.ParsedResults == null || datos.ParsedResults.Count == 0 ||
            datos.ParsedResults[0].TextOverlay == null)
        {
            textoEstado.text = "No se detectó texto";
            yield break;
        }
        
        var lineasOCR = datos.ParsedResults[0].TextOverlay.Lines;
        
        // --- 2. PROCESO DE TRADUCCIÓN ---
        int contador = 0;
        foreach (var lineaOCR in lineasOCR)
        {
            contador++;
            textoEstado.text = $"Traduciendo... {Mathf.Round((float)contador/lineasOCR.Count * 100)}%";
            
            string textoOriginal = lineaOCR.LineText;
            string textoTraducido = "";
            
            // Llamada a Google Translate
            yield return StartCoroutine(TraducirTexto(textoOriginal, (resultado) => {
                textoTraducido = resultado;
            }));
            
            // Cálculo de geometría para saber dónde pintar luego
            float anchoLinea = 0;
            if (lineaOCR.Words != null && lineaOCR.Words.Count > 0)
            {
                float inicio = lineaOCR.Words[0].Left;
                var finWord = lineaOCR.Words[lineaOCR.Words.Count - 1];
                float fin = finWord.Left + finWord.Width;
                anchoLinea = fin - inicio;
            }
            
            // Guardamos el objeto limpio
            lineas.Add(new LineaConTraduccion
            {
                original = textoOriginal,
                traducido = textoTraducido,
                posY = lineaOCR.MinTop,
                posX = lineaOCR.Words != null && lineaOCR.Words.Count > 0 ? lineaOCR.Words[0].Left : 0,
                alto = lineaOCR.MaxHeight,
                ancho = anchoLinea
            });

            // Pequeña pausa para no saturar la API de google si son muchas líneas
            yield return new WaitForSeconds(0.1f); 
        }
        
        // --- 3. FINALIZAR Y MANDAR A RECONSTRUIR ---
        textoEstado.text = ""; // Borramos el texto de estado para que se vea limpio
        
        if (reconstructor != null && imagenEnMemoria != null)
        {
            // Aquí es donde usamos la imagen: Pasamos sus dimensiones (ej. 1920x1080)
            // para que el reconstructor sepa hacer la regla de tres.
            Vector2 dimensiones = new Vector2(imagenEnMemoria.width, imagenEnMemoria.height);
            reconstructor.ReconstruirDocumento(lineas, dimensiones);
        }
        
    }

    IEnumerator TraducirTexto(string texto, System.Action<string> callback)
    {
        string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={idiomaDestino}&dt=t&q={UnityWebRequest.EscapeURL(texto)}";
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                string respuesta = www.downloadHandler.text;
                // Parseo manual rápido del JSON de Google
                int inicio = respuesta.IndexOf("[[\"") + 3;
                int fin = respuesta.IndexOf("\"", inicio);
                if (fin > inicio)
                {
                    string t = respuesta.Substring(inicio, fin - inicio);
                    // Limpieza básica de caracteres escapados
                    t = t.Replace("\\u0027", "'").Replace("\\n", "\n").Replace("\\\"", "\"");
                    callback(t);
                }
                else callback(texto);
            }
            else callback(texto); // Si falla, devuelve el original
        }
    }

    void EscribirLog(string mensaje)
    {
        // Debug.Log solo para desarrollo, puedes quitarlo luego
        Debug.Log("[TRADUCTOR] " + mensaje);
    }

    public void LimpiarDatos()
    {
        lineas.Clear();
        textoEstado.text = "";
        imagenEnMemoria = null;
    }
}

// MANTÉN TUS CLASES SERIALIZABLES ABAJO IGUAL QUE ANTES (RespuestaOCR_V2, etc.)
// ... (Copiar las clases de abajo de tu script anterior)

[Serializable]
public class ListaLineas
{
    public List<LineaConTraduccion> lineas;
}

[Serializable]
public class RespuestaOCR_V2 
{ 
    public List<ParsedResult_V2> ParsedResults; 
}

[Serializable]
public class ParsedResult_V2 
{ 
    public TextOverlay_V2 TextOverlay; 
    public string ParsedText; 
}

[Serializable]
public class TextOverlay_V2 
{ 
    public List<Line_V2> Lines; 
}

[Serializable]
public class Line_V2 
{ 
    public List<Word_V2> Words; 
    public string LineText; 
    public float MaxHeight; 
    public float MinTop; 
}

[Serializable]
public class Word_V2 
{ 
    public string WordText; 
    public float Left; 
    public float Top; 
    public float Height; 
    public float Width; 
}