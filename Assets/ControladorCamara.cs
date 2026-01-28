using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using System.IO;
using System;

public class ControladorCamara : MonoBehaviour
{
    [Header("Referencias UI")]
    public RawImage visorFoto;       
    public AspectRatioFitter ajustadorAspecto; 
    public TextMeshProUGUI textoEstado; 

    private string apiKey = Secretos.OCR_API_KEY;
    private string rutaLog;

    void Start()
    {
        rutaLog = Path.Combine(Application.persistentDataPath, "debug_log.txt");
        EscribirLog("\n\n=== INICIO DE SESIÓN ===");
        EscribirLog($"Ruta log: {rutaLog}");

        if (!Application.isEditor)
        {
            TomarFoto();
        }
        else
        {
            EscribirLog("Modo Editor detectado.");
            textoEstado.text = "Modo Editor: Compila en Android.";
        }
    }

    public void TomarFoto()
    {
        if (NativeCamera.IsCameraBusy())
        {
            EscribirLog("Cámara ocupada");
            return;
        }

        textoEstado.text = "Abriendo cámara...";
        EscribirLog("Solicitando cámara...");
        
        // ✅ CORRECCIÓN: No asignar a variable
        NativeCamera.TakePicture((path) =>
        {
            if (path != null)
            {
                EscribirLog($"Foto tomada en: {path}");
                
                Texture2D textura = NativeCamera.LoadImageAtPath(path, 2048, false);
                
                if(textura != null)
                {
                    EscribirLog($"Textura cargada: {textura.width}x{textura.height}");
                    MostrarFotoEnPantalla(textura);
                    StartCoroutine(ProcesarOCR(textura));
                }
                else
                {
                    EscribirLog("ERROR: NativeCamera devolvió textura nula.");
                    textoEstado.text = "Error cargando foto.";
                }
            }
            else
            {
                EscribirLog("Usuario canceló la foto.");
                textoEstado.text = "Cancelado.";
            }
        }, maxSize: 2048);
        
        EscribirLog("Cámara solicitada");
    }

    void MostrarFotoEnPantalla(Texture2D textura)
    {
        visorFoto.texture = textura;

        if (ajustadorAspecto != null)
        {
            float ratio = (float)textura.width / (float)textura.height;
            ajustadorAspecto.aspectRatio = ratio;
        }
    }

    IEnumerator ProcesarOCR(Texture2D texturaOriginal)
    {
        textoEstado.text = "Comprimiendo imagen...";
        EscribirLog("Comprimiendo imagen...");
        
        // Comprimir a JPG con calidad 85
        byte[] imagenBytes = texturaOriginal.EncodeToJPG(85);
        
        float tamanoKB = imagenBytes.Length / 1024f;
        EscribirLog($"Tamaño imagen: {tamanoKB:F2} KB ({imagenBytes.Length} bytes)");

        // Verificar límite de 1MB (la API gratis tiene este límite)
        if(imagenBytes.Length > 1024 * 1024)
        {
            EscribirLog("Imagen > 1MB, recomprimiendo a calidad 60...");
            imagenBytes = texturaOriginal.EncodeToJPG(60);
            tamanoKB = imagenBytes.Length / 1024f;
            EscribirLog($"Nuevo tamaño: {tamanoKB:F2} KB");
        }

        textoEstado.text = "Enviando a OCR...";
        EscribirLog("Preparando petición HTTP...");

        // Crear formulario
        WWWForm formulario = new WWWForm();
        formulario.AddBinaryData("file", imagenBytes, "imagen.jpg", "image/jpeg");
        formulario.AddField("language", "spa");
        formulario.AddField("isOverlayRequired", "false");
        formulario.AddField("scale", "true");
        formulario.AddField("OCREngine", "2");
        formulario.AddField("detectOrientation", "true");

        EscribirLog("Enviando petición a OCR.space...");

        UnityWebRequest www = UnityWebRequest.Post("https://api.ocr.space/parse/image", formulario);
        
        // ✅ API key en el HEADER
        www.SetRequestHeader("apikey", apiKey);
        www.timeout = 30;

        yield return www.SendWebRequest();

        EscribirLog($"Respuesta HTTP: {www.responseCode}");

        if (www.result != UnityWebRequest.Result.Success)
        {
            string errorMsg = $"Error: {www.error} (Code: {www.responseCode})";
            EscribirLog(errorMsg);
            
            if(www.downloadHandler != null && !string.IsNullOrEmpty(www.downloadHandler.text))
            {
                EscribirLog($"Detalle servidor: {www.downloadHandler.text}");
            }
            
            textoEstado.text = $"Error {www.responseCode}:\n{www.error}";
        }
        else
        {
            string respuestaJson = www.downloadHandler.text;
            EscribirLog($"Respuesta recibida ({respuestaJson.Length} caracteres)");
            EscribirLog($"Primeros 200 chars: {respuestaJson.Substring(0, Mathf.Min(200, respuestaJson.Length))}");
            
            // Guardar JSON completo
            string rutaJson = Path.Combine(Application.persistentDataPath, "ultima_respuesta.json");
            File.WriteAllText(rutaJson, respuestaJson);
            EscribirLog($"JSON guardado en: {rutaJson}");
            
            ProcesarRespuestaJSON(respuestaJson);
        }
    }

    void ProcesarRespuestaJSON(string json)
    {
        EscribirLog("Procesando JSON...");
    
    try
    {
        // ✅ CORRECCIÓN: Verificar que IsErroredOnProcessing sea true
        if (json.Contains("\"IsErroredOnProcessing\":true"))
        {
            EscribirLog("API reportó error: IsErroredOnProcessing = true");
            
            // Intentar extraer el mensaje de error
            if (json.Contains("\"ErrorMessage\":\""))
            {
                int errorInicio = json.IndexOf("\"ErrorMessage\":\"") + 16;
                int errorFin = json.IndexOf("\"", errorInicio);
                if (errorFin > errorInicio)
                {
                    string errorMsg = json.Substring(errorInicio, errorFin - errorInicio);
                    EscribirLog($"Mensaje de error: {errorMsg}");
                }
            }
            
            textoEstado.text = "Error: La API no pudo procesar la imagen";
            return;
        }

        // Buscar ParsedText
        if (json.Contains("\"ParsedText\""))
        {
            int inicio = json.IndexOf("\"ParsedText\":\"") + 14;
            int fin = inicio;
            bool escape = false;
            
            for (int i = inicio; i < json.Length; i++)
            {
                if (json[i] == '\\' && !escape)
                {
                    escape = true;
                    continue;
                }
                
                if (json[i] == '"' && !escape)
                {
                    fin = i;
                    break;
                }
                
                escape = false;
            }
            
            if (fin > inicio)
            {
                string textoExtraido = json.Substring(inicio, fin - inicio);
                
                // Limpiar caracteres de escape
                textoExtraido = textoExtraido.Replace("\\r\\n", "\n");
                textoExtraido = textoExtraido.Replace("\\n", "\n");
                textoExtraido = textoExtraido.Replace("\\t", " ");
                textoExtraido = textoExtraido.Trim();
                
                EscribirLog($"Texto extraído ({textoExtraido.Length} caracteres):");
                EscribirLog(textoExtraido);
                
                if (string.IsNullOrWhiteSpace(textoExtraido))
                {
                    EscribirLog("ParsedText está vacío");
                    textoEstado.text = "No se detectó texto en la imagen";
                }
                else
                {
                    EscribirLog("✓ Texto mostrado en pantalla");
                    textoEstado.text = textoExtraido;
                }
            }
            else
            {
                EscribirLog("No se pudo extraer ParsedText del JSON");
                textoEstado.text = "Error al procesar respuesta";
            }
        }
        else
        {
            EscribirLog("JSON no contiene ParsedText");
            textoEstado.text = "Respuesta inválida del servidor";
        }
    }
    catch (Exception e)
    {
        EscribirLog($"Excepción: {e.Message}");
        EscribirLog($"Stack: {e.StackTrace}");
        textoEstado.text = "Error procesando respuesta";
    }
}
    void EscribirLog(string mensaje)
    {
        string logLine = $"[{DateTime.Now:HH:mm:ss}] {mensaje}\n";
        Debug.Log(mensaje); 
        
        try 
        {
            File.AppendAllText(rutaLog, logLine);
        } 
        catch (Exception e)
        {
            Debug.LogError($"Error escribiendo log: {e.Message}");
        }
    }

    public void Reintentar()
    {
        TomarFoto();
    }
}