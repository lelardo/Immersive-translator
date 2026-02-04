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
    public TextMeshProUGUI textoEstado;
    
    [Header("Referencias IA")]
    public GeminiAdapter adaptadorGemini; // ← ESTO FALTABA
    
    [Header("Reconstructor")]
    public ReconstructorDocumento reconstructor;

    [Header("Configuración Interna")]
    private string idiomaDestino = "es";
    private string modoOperacion = "Traduccion"; // ← ESTO FALTABA
    
    private string rutaLog;
    private List<LineaConTraduccion> lineas = new List<LineaConTraduccion>();
    private Texture2D imagenEnMemoria;

    void Start()
    {
        // 1. LEER CONFIGURACIÓN DEL MENÚ
        if (PlayerPrefs.HasKey("IdiomaDestino"))
            idiomaDestino = PlayerPrefs.GetString("IdiomaDestino");
        
        if (PlayerPrefs.HasKey("ModoApp"))
            modoOperacion = PlayerPrefs.GetString("ModoApp"); // ← ESTO FALTABA

        Debug.Log($"[TRADUCTOR] Iniciado. Modo: {modoOperacion} -> Idioma: {idiomaDestino}");

        rutaLog = Path.Combine(Application.persistentDataPath, "traductor_log.txt");
        if(textoEstado) textoEstado.text = "";
        
        // Conectar eventos de Gemini
        if (adaptadorGemini != null)
        {
            adaptadorGemini.OnStatusUpdate += (mensaje) => {
                if (textoEstado != null) textoEstado.text = mensaje;
            };
        }
    }

    public void ProcesarOCR(string jsonOCR, Texture2D imagen)
    {
        EscribirLog("=== Datos recibidos del OCR ===");
        imagenEnMemoria = imagen;
        
        // Parsear JSON
        RespuestaOCR_V2 respuesta = JsonUtility.FromJson<RespuestaOCR_V2>(jsonOCR);
        
        if (respuesta == null || respuesta.ParsedResults == null || respuesta.ParsedResults.Count == 0)
        {
            if(textoEstado) textoEstado.text = "✗ No detecté texto.";
            return;
        }

        // Llenar listas
        lineas.Clear();
        List<string> textosPuros = new List<string>();
        
        foreach (var pr in respuesta.ParsedResults)
        {
            if (pr.TextOverlay == null) continue;
            
            foreach (var l in pr.TextOverlay.Lines)
            {
                if (l.Words == null || l.Words.Count == 0) continue;
                
                LineaConTraduccion nueva = new LineaConTraduccion();
                nueva.original = l.LineText;
                nueva.posY = l.MinTop;
                nueva.posX = l.Words[0].Left;
                nueva.alto = l.MaxHeight;
                
                float inicio = l.Words[0].Left;
                var finWord = l.Words[l.Words.Count - 1];
                nueva.ancho = (finWord.Left + finWord.Width) - inicio;
                
                lineas.Add(nueva);
                textosPuros.Add(l.LineText);
            }
        }

        if (lineas.Count == 0)
        {
            if(textoEstado) textoEstado.text = "✗ No se detectó texto";
            return;
        }

        // ✅ DECISIÓN CLAVE: ¿Qué modo usar?
        if (modoOperacion == "Traduccion")
        {
            // Google Translate
            StartCoroutine(ProcesoGoogleTranslate(textosPuros, imagen));
        }
        else
        {
            // Gemini
            if (adaptadorGemini != null)
            {
                StartCoroutine(adaptadorGemini.SolicitarAdaptacion(textosPuros, idiomaDestino, modoOperacion, (resultados) => 
                {
                    if (resultados != null)
                    {
                        for(int i = 0; i < lineas.Count; i++)
                        {
                            if(i < resultados.Count)
                                lineas[i].traducido = resultados[i];
                        }
                        
                        if (reconstructor != null && imagenEnMemoria != null)
                        {
                            reconstructor.ReconstruirDocumento(lineas, new Vector2(imagenEnMemoria.width, imagenEnMemoria.height));
                        }
                        
                        if(textoEstado) textoEstado.text = "✅ ¡Listo!";
                    }
                    else
                    {
                        if(textoEstado) textoEstado.text = "⚠️ Falló Gemini.";
                    }
                }));
            }
            else
            {
                if(textoEstado) textoEstado.text = "✗ Falta GeminiAdapter";
            }
        }
    }

    IEnumerator ProcesoGoogleTranslate(List<string> textos, Texture2D textura)
    {
        int contador = 0;
        
        foreach(var linea in lineas)
        {
            bool termino = false;
            
            StartCoroutine(CorutinaTraducirGoogle(linea.original, (trad) => {
                linea.traducido = trad;
                termino = true;
            }));
            
            yield return new WaitUntil(() => termino);
            
            contador++;
            
            if(textoEstado)
                textoEstado.text = $"Traduciendo... {Mathf.Round((float)contador / lineas.Count * 100)}%";
            
            yield return new WaitForSeconds(0.1f);
        }
        
        // ✅ DIBUJAR
        if (reconstructor != null && textura != null)
        {
            reconstructor.ReconstruirDocumento(lineas, new Vector2(textura.width, textura.height));
            if(textoEstado) textoEstado.text = "✅ Traducción completa";
        }
    }

    IEnumerator CorutinaTraducirGoogle(string texto, System.Action<string> callback)
    {
        string url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl=" + idiomaDestino + "&dt=t&q=" + UnityWebRequest.EscapeURL(texto);
        
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
                else
                {
                    callback(texto);
                }
            }
            else
            {
                callback(texto);
            }
        }
    }

    void EscribirLog(string msg)
    {
        Debug.Log("[TRADUCTOR] " + msg);
    }
}

// CLASES SERIALIZABLES
[Serializable] public class RespuestaOCR_V2 { public List<ParsedResult_V2> ParsedResults; }
[Serializable] public class ParsedResult_V2 { public TextOverlay_V2 TextOverlay; public string ParsedText; }
[Serializable] public class TextOverlay_V2 { public List<Line_V2> Lines; }
[Serializable] public class Line_V2 
{ 
    public List<Word_V2> Words; 
    public string LineText; 
    public float MaxHeight;
    public float MinTop;
}
[Serializable] public class Word_V2 { public string WordText; public float Left; public float Top; public float Height; public float Width; }
