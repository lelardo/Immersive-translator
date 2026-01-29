using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using System;

public class ControladorOCR : MonoBehaviour
{
    [Header("Referencia al Traductor")]
    public TraductorTexto traductor; // ← Arrastra el otro script aquí
    
    private string apiKey = Secretos.OCR_API_KEY;
    private string rutaLog;
    
    void Start()
    {
        rutaLog = Path.Combine(Application.persistentDataPath, "ocr_log.txt");
        EscribirLog("\n\n=== OCR INICIADO ===");
    }

    public void IniciarEscaneo()
    {
        if (NativeCamera.IsCameraBusy())
        {
            EscribirLog("Cámara ocupada");
            return;
        }

        EscribirLog("Abriendo cámara...");
        
        NativeCamera.TakePicture((path) =>
        {
            if (path != null)
            {
                EscribirLog($"Foto capturada: {path}");
                
                Texture2D textura = NativeCamera.LoadImageAtPath(path, 2048, false);
                
                if(textura != null)
                {
                    EscribirLog($"Textura cargada: {textura.width}x{textura.height}");
                    StartCoroutine(ProcesarOCR(textura));
                }
                else
                {
                    EscribirLog("ERROR: Textura nula");
                }
            }
            else
            {
                EscribirLog("Foto cancelada");
            }
        }, maxSize: 2048);
    }

    IEnumerator ProcesarOCR(Texture2D textura)
    {
        EscribirLog("Comprimiendo imagen...");
        
        byte[] imagenBytes = textura.EncodeToJPG(85);
        float tamanoKB = imagenBytes.Length / 1024f;
        EscribirLog($"Tamaño: {tamanoKB:F2} KB");

        if(imagenBytes.Length > 1024 * 1024)
        {
            imagenBytes = textura.EncodeToJPG(60);
            EscribirLog($"Recomprimido: {imagenBytes.Length / 1024f:F2} KB");
        }

        EscribirLog("Enviando a OCR.space...");
        
        WWWForm formulario = new WWWForm();
        formulario.AddBinaryData("file", imagenBytes, "imagen.jpg", "image/jpeg");
        formulario.AddField("language", "spa");
        formulario.AddField("isOverlayRequired", "true"); // Importante para coordenadas
        formulario.AddField("scale", "true");
        formulario.AddField("OCREngine", "2");
        formulario.AddField("detectOrientation", "true");

        UnityWebRequest www = UnityWebRequest.Post("https://api.ocr.space/parse/image", formulario);
        www.SetRequestHeader("apikey", apiKey);
        www.timeout = 30;

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            EscribirLog($"Error OCR: {www.error} ({www.responseCode})");
            
            if(www.downloadHandler != null)
            {
                EscribirLog($"Detalle: {www.downloadHandler.text}");
            }
        }
        else
        {
            string json = www.downloadHandler.text;
            EscribirLog($"OCR exitoso ({json.Length} caracteres)");
            
            // Guardar JSON
            string rutaJson = Path.Combine(Application.persistentDataPath, "ocr_resultado.json");
            File.WriteAllText(rutaJson, json);
            EscribirLog($"JSON guardado: {rutaJson}");

            
            
            // Enviar datos al traductor
            if (traductor != null)
            {
                EscribirLog("Enviando datos al traductor...");
                traductor.ProcesarOCR(json, textura);
            }
            else
            {
                EscribirLog("ERROR: No hay referencia al traductor");
            }
        }
    }

    void EscribirLog(string mensaje)
    {
        string logLine = $"[{DateTime.Now:HH:mm:ss}] {mensaje}\n";
        Debug.Log("[OCR] " + mensaje);
        
        try 
        {
            File.AppendAllText(rutaLog, logLine);
        } 
        catch { }
    }
}