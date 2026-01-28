using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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
    public RawImage visorFoto;
    public AspectRatioFitter ajustadorAspecto;
    public TextMeshProUGUI textoEstado;
    
    [Header("Configuración")]
    public string idiomaDestino = "en"; // en, fr, de, etc.
    
    private string rutaLog;
    private List<LineaConTraduccion> lineas = new List<LineaConTraduccion>();
    private Texture2D imagenActual;

    void Start()
    {
        rutaLog = Path.Combine(Application.persistentDataPath, "traductor_log.txt");
        EscribirLog("\n\n=== TRADUCTOR INICIADO ===");
        textoEstado.text = "Presiona el botón\npara escanear documento";
    }

    public void ProcesarOCR(string jsonOCR, Texture2D imagen)
    {
        EscribirLog("=== Datos recibidos del OCR ===");
        imagenActual = imagen;
        
        MostrarImagen(imagen);
        StartCoroutine(ExtraerYTraducir(jsonOCR));
    }

    void MostrarImagen(Texture2D textura)
    {
        visorFoto.texture = textura;

        if (ajustadorAspecto != null)
        {
            float ratio = (float)textura.width / (float)textura.height;
            ajustadorAspecto.aspectRatio = ratio;
        }
        
        EscribirLog($"Imagen mostrada: {textura.width}x{textura.height}");
    }

    IEnumerator ExtraerYTraducir(string json)
    {
        textoEstado.text = "Extrayendo texto...";
        lineas.Clear();
        
        // Verificar errores del OCR
        if (json.Contains("\"IsErroredOnProcessing\":true"))
        {
            EscribirLog("OCR reportó error");
            textoEstado.text = "Error: OCR falló";
            yield break;
        }
        
        // Parsear JSON
        RespuestaOCR_V2 datos = JsonUtility.FromJson<RespuestaOCR_V2>(json);
        
        if (datos == null || datos.ParsedResults == null || datos.ParsedResults.Count == 0)
        {
            EscribirLog("JSON inválido o vacío");
            textoEstado.text = "Error: Datos inválidos";
            yield break;
        }
        
        if (datos.ParsedResults[0].TextOverlay == null || datos.ParsedResults[0].TextOverlay.Lines == null)
        {
            EscribirLog("No hay líneas en el JSON");
            textoEstado.text = "No se detectó texto";
            yield break;
        }
        
        var lineasOCR = datos.ParsedResults[0].TextOverlay.Lines;
        EscribirLog($"Líneas detectadas: {lineasOCR.Count}");
        
        textoEstado.text = $"Traduciendo {lineasOCR.Count} líneas...";
        
        int contador = 0;
        foreach (var lineaOCR in lineasOCR)
        {
            contador++;
            textoEstado.text = $"Traduciendo {contador}/{lineasOCR.Count}...";
            
            string textoOriginal = lineaOCR.LineText;
            string textoTraducido = "";
            
            // Traducir
            yield return StartCoroutine(TraducirTexto(textoOriginal, (resultado) => {
                textoTraducido = resultado;
            }));
            
            // Calcular ancho de la línea
            float anchoLinea = 0;
            if (lineaOCR.Words != null && lineaOCR.Words.Count > 0)
            {
                float inicioPalabra = lineaOCR.Words[0].Left;
                var ultimaPalabra = lineaOCR.Words[lineaOCR.Words.Count - 1];
                float finPalabra = ultimaPalabra.Left + ultimaPalabra.Width;
                anchoLinea = finPalabra - inicioPalabra;
            }
            
            // Guardar línea
            LineaConTraduccion linea = new LineaConTraduccion
            {
                original = textoOriginal,
                traducido = textoTraducido,
                posY = lineaOCR.MinTop,
                posX = lineaOCR.Words != null && lineaOCR.Words.Count > 0 ? lineaOCR.Words[0].Left : 0,
                alto = lineaOCR.MaxHeight,
                ancho = anchoLinea
            };
            
            lineas.Add(linea);
            EscribirLog($"[{contador}] '{textoOriginal}' → '{textoTraducido}'");
            
            yield return new WaitForSeconds(0.3f);
        }
        
        MostrarResultado();
    }

    IEnumerator TraducirTexto(string texto, System.Action<string> callback)
    {
        // Google Translate API no oficial
        string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={idiomaDestino}&dt=t&q={UnityWebRequest.EscapeURL(texto)}";
        
        UnityWebRequest www = UnityWebRequest.Get(url);
        
        yield return www.SendWebRequest();
        
        if (www.result == UnityWebRequest.Result.Success)
        {
            string respuesta = www.downloadHandler.text;
            
            int inicio = respuesta.IndexOf("[[\"") + 3;
            int fin = respuesta.IndexOf("\"", inicio);
            
            if (fin > inicio)
            {
                string traduccion = respuesta.Substring(inicio, fin - inicio);
                
                traduccion = traduccion.Replace("\\u0027", "'");
                traduccion = traduccion.Replace("\\n", "\n");
                traduccion = traduccion.Replace("\\\"", "\"");
                
                callback(traduccion);
            }
            else
            {
                EscribirLog($"No se pudo parsear traducción de: '{texto}'");
                callback(texto);
            }
        }
        else
        {
            EscribirLog($"Error traduciendo '{texto}': {www.error}");
            callback(texto);
        }
    }

    void MostrarResultado()
    {
        string textoCompleto = "";
        
        foreach (var linea in lineas)
        {
            textoCompleto += linea.traducido + "\n";
        }
        
        textoEstado.text = textoCompleto.Trim();
        EscribirLog($"✓ Traducción completa ({lineas.Count} líneas)");
        
        GuardarDatosTraduccion();
    }

    void GuardarDatosTraduccion()
    {
        string jsonLineas = JsonUtility.ToJson(new ListaLineas { lineas = lineas }, true);
        string ruta = Path.Combine(Application.persistentDataPath, "traduccion_final.json");
        File.WriteAllText(ruta, jsonLineas);
        EscribirLog($"Datos guardados: {ruta}");
    }

    void EscribirLog(string mensaje)
    {
        string logLine = $"[{DateTime.Now:HH:mm:ss}] {mensaje}\n";
        Debug.Log("[TRADUCTOR] " + mensaje);
        
        try 
        {
            File.AppendAllText(rutaLog, logLine);
        } 
        catch { }
    }
    
    public void LimpiarDatos()
    {
        lineas.Clear();
        textoEstado.text = "Presiona el botón\npara escanear documento";
        visorFoto.texture = null;
    }
}

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