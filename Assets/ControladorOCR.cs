using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using System;

public class ControladorOCR : MonoBehaviour
{
    [Header("Referencia al Traductor")]
    public TraductorTexto traductor;
    public ControladorAR controladorAR;
    private string apiKey = Secretos.OCR_API_KEY;
    private string rutaLog;
    private string rutaEstado; // Nuevo: archivo de estado
    
    void Awake()
    {
        // ✅ Mantener este GameObject vivo entre escenas
        DontDestroyOnLoad(gameObject);
    }
    
    void Start()
    {
        rutaLog = Path.Combine(Application.persistentDataPath, "ocr_log.txt");
        rutaEstado = Path.Combine(Application.persistentDataPath, "estado_app.txt");
        
        EscribirLog("\n\n=== OCR INICIADO ===");
        
        // VERIFICAR SI VOLVIMOS DE LA CÁMARA
        if (File.Exists(rutaEstado))
        {
            try
            {
                string rutaFoto = File.ReadAllText(rutaEstado);
                EscribirLog($"⚠️ Detectado reinicio. Ruta guardada: {rutaFoto}");
                
                // Limpiar archivo de estado para que no se cicle
                File.Delete(rutaEstado);
                
                if (!string.IsNullOrEmpty(rutaFoto) && File.Exists(rutaFoto))
                {
                    // --- CORRECCIÓN CLAVE AQUÍ ---
                    // Le gritamos al ControladorAR: "¡Ya volví! ¡Enciende el cursor!"
                    if (controladorAR != null)
                    {
                        EscribirLog("Notificando al AR que active el cursor...");
                        controladorAR.NotificarRegresoDeCamara(); 
                    }
                    else
                    {
                        EscribirLog("ERROR: ControladorAR no asignado en el Inspector.");
                    }
                    // -----------------------------

                    EscribirLog("Cargando textura guardada...");
                    Texture2D textura = NativeCamera.LoadImageAtPath(rutaFoto, 2048, false);
                    
                    if(textura != null)
                    {
                        StartCoroutine(ProcesarOCR(textura));
                    }
                }
            }
            catch (Exception e)
            {
                EscribirLog($"Error recuperando estado: {e.Message}");
            }
        }
    }
        
    void ReiniciarFlujo()
    {
        EscribirLog("Reiniciando flujo desde cero...");
        Invoke("IniciarEscaneo", 1f);
    }

    public void IniciarEscaneo()
    {
        if (NativeCamera.IsCameraBusy())
        {
            EscribirLog("Cámara ocupada, reintentando...");
            Invoke("IniciarEscaneo", 0.5f);
            return;
        }

        EscribirLog("=== Abriendo cámara nativa ===");
        
        NativeCamera.TakePicture((path) =>
        {
            EscribirLog($"=== Callback ejecutado ===");
            EscribirLog($"Path recibido: {(path != null ? path : "NULL")}");
            
            if (path != null)
            {
                EscribirLog($"✓ Foto capturada: {path}");
                
                // ✅ GUARDAR ruta en archivo ANTES de procesar
                // Por si Android mata la app
                try
                {
                    File.WriteAllText(rutaEstado, path);
                    EscribirLog($"Estado guardado en: {rutaEstado}");
                }
                catch (Exception e)
                {
                    EscribirLog($"✗ Error guardando estado: {e.Message}");
                }
                
                // Cargar textura
                Texture2D textura = NativeCamera.LoadImageAtPath(path, 2048, false);
                
                if(textura != null)
                {
                    EscribirLog($"✓ Textura cargada: {textura.width}x{textura.height}");
                    
                    // Borrar estado porque ya tenemos la textura
                    if (File.Exists(rutaEstado))
                    {
                        File.Delete(rutaEstado);
                        EscribirLog("Estado limpiado (procesando inmediatamente)");
                    }
                    if (controladorAR != null) 
                    {
                        EscribirLog("Regreso normal de cámara: Activando AR...");
                        controladorAR.NotificarRegresoDeCamara();
                    }
                    
                    StartCoroutine(ProcesarOCR(textura));
                }
                else
                {
                    EscribirLog("✗ ERROR: Textura nula");
                }
            }
            else
            {
                EscribirLog("✗ Usuario canceló la foto");
                
                // Limpiar estado si existe
                if (File.Exists(rutaEstado))
                {
                    File.Delete(rutaEstado);
                }
            }
        }, maxSize: 2048);
        
        EscribirLog("Esperando que usuario tome foto...");
    }

    IEnumerator ProcesarOCR(Texture2D textura)
    {
        EscribirLog("=== Iniciando procesamiento OCR ===");
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
        formulario.AddField("isOverlayRequired", "true");
        formulario.AddField("scale", "true");
        formulario.AddField("OCREngine", "2");
        formulario.AddField("detectOrientation", "true");

        UnityWebRequest www = UnityWebRequest.Post("https://api.ocr.space/parse/image", formulario);
        www.SetRequestHeader("apikey", apiKey);
        www.timeout = 30;

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            EscribirLog($"✗ Error OCR: {www.error} ({www.responseCode})");
            
            if(www.downloadHandler != null)
            {
                EscribirLog($"Detalle: {www.downloadHandler.text}");
            }
        }
        else
        {
            string json = www.downloadHandler.text;
            EscribirLog($"✓ OCR exitoso ({json.Length} caracteres)");
            
            string rutaJson = Path.Combine(Application.persistentDataPath, "ocr_resultado.json");
            File.WriteAllText(rutaJson, json);
            EscribirLog($"JSON guardado: {rutaJson}");
            
            if (traductor != null)
            {
                EscribirLog("Enviando datos al traductor...");
                traductor.ProcesarOCR(json, textura);
            }
            else
            {
                EscribirLog("✗ ERROR: No hay referencia al traductor");
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
    
    // Para limpiar estado manualmente si algo sale mal
    void OnApplicationQuit()
    {
        if (File.Exists(rutaEstado))
        {
            File.Delete(rutaEstado);
            EscribirLog("Estado limpiado al cerrar app");
        }
    }
}