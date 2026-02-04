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
    public float alto; // <--- ESTO ES LO IMPORTANTE PARA EL TAMAÑO DE LETRA
    public float ancho;
}

public class TraductorTexto : MonoBehaviour
{
    
    [Header("Referencias UI")]
    public TextMeshProUGUI textoEstado;
    
    [Header("Configuración")]
    public string idiomaDestino = "es"; // Sugerencia: Cambiado a 'es' por defecto
    
    private string rutaLog;
    private List<LineaConTraduccion> lineas = new List<LineaConTraduccion>();
    private Texture2D imagenEnMemoria; 

    [Header("Reconstructor")]
    public ReconstructorDocumento reconstructor;

    // void Awake() // <--- MOVIDO AQUÍ (Lugar correcto)
    // {
    //     DontDestroyOnLoad(gameObject);
    // }

    void Start()
    {
        // ---------------------------------------------------------
        // 1. LEER LA CONFIGURACIÓN DEL MENÚ
        // ---------------------------------------------------------
        if (PlayerPrefs.HasKey("IdiomaDestino"))
        {
            // Sobreescribimos la variable pública con lo que eligió el usuario
            idiomaDestino = PlayerPrefs.GetString("IdiomaDestino");
            Debug.Log($"[TRADUCTOR] Idioma cargado desde menú: {idiomaDestino}");
        }
        else
        {
            Debug.LogWarning("[TRADUCTOR] No se encontró configuración, usando idioma por defecto: " + idiomaDestino);
        }

        // ---------------------------------------------------------
        // 2. CONFIGURACIÓN RESTANTE (Lo que ya tenías)
        // ---------------------------------------------------------
        rutaLog = Path.Combine(Application.persistentDataPath, "traductor_log.txt");
        if(textoEstado) textoEstado.text = ""; 
    }

    public void ProcesarOCR(string jsonOCR, Texture2D imagen)
    {
        EscribirLog("=== Datos recibidos del OCR ===");
        imagenEnMemoria = imagen; 
        StartCoroutine(ExtraerYTraducir(jsonOCR));
    }

    IEnumerator ExtraerYTraducir(string json)
    {
        if(textoEstado) textoEstado.text = "Procesando...";
        lineas.Clear();
        
        // --- 1. VALIDACIONES ---
        if (string.IsNullOrEmpty(json) || json.Contains("\"IsErroredOnProcessing\":true"))
        {
            if(textoEstado) textoEstado.text = "Error en lectura OCR";
            yield break;
        }

        RespuestaOCR_V2 datos = null;
        try 
        {
            datos = JsonUtility.FromJson<RespuestaOCR_V2>(json);
        }
        catch (Exception) { }

        if (datos == null || datos.ParsedResults == null || datos.ParsedResults.Count == 0 ||
            datos.ParsedResults[0].TextOverlay == null)
        {
            if(textoEstado) textoEstado.text = "No se detectó texto";
            yield break;
        }

        var lineasOCR = datos.ParsedResults[0].TextOverlay.Lines;
        
        // --- 2. PROCESO DE TRADUCCIÓN ---
        int contador = 0;
        foreach (var lineaOCR in lineasOCR)
        {
            contador++;
            if(textoEstado) textoEstado.text = $"Traduciendo... {Mathf.Round((float)contador/lineasOCR.Count * 100)}%";
            
            string textoOriginal = lineaOCR.LineText;
            string textoTraducido = "";
            
            // Llamada a Google Translate
            yield return StartCoroutine(TraducirTexto(textoOriginal, (resultado) => {
                textoTraducido = resultado;
            }));
            
            // Cálculo de ancho (aunque para la lista vertical no es crítico, lo mantenemos)
            float anchoLinea = 0;
            float posX = 0;
            if (lineaOCR.Words != null && lineaOCR.Words.Count > 0)
            {
                posX = lineaOCR.Words[0].Left;
                var finWord = lineaOCR.Words[lineaOCR.Words.Count - 1];
                anchoLinea = (finWord.Left + finWord.Width) - posX;
            }
            
            // GUARDAMOS LOS DATOS
            lineas.Add(new LineaConTraduccion
            {
                original = textoOriginal,
                traducido = textoTraducido,
                posY = lineaOCR.MinTop,
                posX = posX,
                alto = lineaOCR.MaxHeight, // <--- AQUÍ CAPTURAMOS LA ALTURA DE LA FUENTE
                ancho = anchoLinea
            });

            yield return new WaitForSeconds(0.1f); 
        }
        
        // --- 3. MANDAR A RECONSTRUIR ---
        if(textoEstado) textoEstado.text = ""; 
        
        if (reconstructor != null)
        {
            // Pasamos dimensiones genéricas si no hay imagen, para evitar error matemático
            Vector2 dimensiones = (imagenEnMemoria != null) 
                ? new Vector2(imagenEnMemoria.width, imagenEnMemoria.height) 
                : new Vector2(1000, 1000);

            reconstructor.ReconstruirDocumento(lineas, dimensiones);
        }
    }

    // (El resto de tus funciones TraducirTexto, EscribirLog, LimpiarDatos siguen igual...)
    // ... COPIA TUS MISMAS FUNCIONES AQUÍ ABAJO ...
    // ...
    // ...

    IEnumerator TraducirTexto(string texto, System.Action<string> callback)
    {
        string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={idiomaDestino}&dt=t&q={UnityWebRequest.EscapeURL(texto)}";
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                string respuesta = www.downloadHandler.text;
                int inicio = respuesta.IndexOf("[[\"") + 3;
                int fin = respuesta.IndexOf("\"", inicio);
                if (fin > inicio)
                {
                    string t = respuesta.Substring(inicio, fin - inicio);
                    t = t.Replace("\\u0027", "'").Replace("\\n", "\n").Replace("\\\"", "\"");
                    callback(t);
                }
                else callback(texto);
            }
            else callback(texto);
        }
    }

    void EscribirLog(string msg) { Debug.Log("[TRADUCTOR] " + msg); }
    public void LimpiarDatos() { lineas.Clear(); if(textoEstado) textoEstado.text = ""; imagenEnMemoria = null; }
}

// TUS CLASES SERIALIZABLES (CORRECTAS)
[Serializable] public class ListaLineas { public List<LineaConTraduccion> lineas; }
[Serializable] public class RespuestaOCR_V2 { public List<ParsedResult_V2> ParsedResults; }
[Serializable] public class ParsedResult_V2 { public TextOverlay_V2 TextOverlay; public string ParsedText; }
[Serializable] public class TextOverlay_V2 { public List<Line_V2> Lines; }
[Serializable] public class Line_V2 { 
    public List<Word_V2> Words; 
    public string LineText; 
    public float MaxHeight; // Vital para el tamaño de letra
    public float MinTop; 
}
[Serializable] public class Word_V2 { public string WordText; public float Left; public float Top; public float Height; public float Width; }