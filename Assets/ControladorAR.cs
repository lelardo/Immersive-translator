using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System.Collections;
using TMPro; // Para el texto de UI

public class ControladorAR : MonoBehaviour
{
    [Header("AR Components")]
    public ARPlaneManager planeManager;
    public ARRaycastManager raycastManager;
    public GameObject cursorGuia;
    public GameObject panelReconstruccion;

    [Header("UI")]
    public GameObject botonEscanear;
    public TextMeshProUGUI textoInstrucciones; // Un texto arriba que diga qué hacer

    // Variables internas
    private bool esperandoParaColocar = false;
    private Texture2D fotoCapturada;
    
    // Referencia a tu traductor
    public TraductorTexto miTraductor; 

    void Start()
    {
        // 1. AL INICIO: Apagar todo lo de AR para no gastar recursos ni confundir
        planeManager.enabled = false; 
        cursorGuia.SetActive(false);
        panelReconstruccion.SetActive(false);
        
        // Ocultar los planos feos si habían quedado
        OcultarPlanos();

        textoInstrucciones.text = "Presiona Escanear para comenzar";
        
    }

    // --- PASO A: ESTO LO LLAMA TU BOTÓN "ESCANEAR" ---
    public void IniciarProceso()
    {
        // Aquí llamas a tu script de cámara nativa.
        // Simulamos que al volver, recibes la foto en la función "RecibirFoto"
        
        // EJEMPLO (Pseudocódigo de tu cámara nativa):
        // NativeCamera.TakePicture((path) => {
        //     Texture2D textura = NativeCamera.LoadImageAtPath(path);
        //     RecibirFoto(textura);
        // });
        
        Debug.Log("Abriendo cámara nativa...");
    }

    // --- PASO B: AL VOLVER DE LA CÁMARA ---
    public void RecibirFoto(Texture2D foto)
    {
        fotoCapturada = foto;
        
        // 1. Mandar a traducir inmediatamente en segundo plano
        if(miTraductor != null) miTraductor.ProcesarOCR("json_falso_aqui", foto); // Ajusta según tu script

        // 2. Activar el cerebro AR recién ahora
        planeManager.enabled = true; // ¡DESPIERTA AR!
        esperandoParaColocar = true;
        
        // 3. Actualizar UI
        botonEscanear.SetActive(false);
        textoInstrucciones.text = "¡Foto lista! Apunta a la mesa y toca para ver la traducción";
    }

    void Update()
    {
        // Solo trabajamos si ya volvimos de la cámara y estamos buscando mesa
        if (!esperandoParaColocar) return;

        ActualizarCursor();

        // Si toca la pantalla y el cursor está visible...
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            if (cursorGuia.activeSelf)
            {
                ColocarDocumentoFinal();
            }
        }
    }

    void ActualizarCursor()
    {
        // Mismo código de raycast que arreglamos antes
        var centro = new Vector2(Screen.width / 2, Screen.height / 2);
        var hits = new System.Collections.Generic.List<ARRaycastHit>();

        if (raycastManager.Raycast(centro, hits, UnityEngine.XR.ARSubsystems.TrackableType.PlaneWithinPolygon))
        {
            cursorGuia.SetActive(true);
            cursorGuia.transform.position = hits[0].pose.position;
            cursorGuia.transform.rotation = hits[0].pose.rotation;
        }
        else
        {
            cursorGuia.SetActive(false);
        }
    }

    void ColocarDocumentoFinal()
    {
        // 1. Mover el panel al cursor
        panelReconstruccion.SetActive(true);
        panelReconstruccion.transform.position = cursorGuia.transform.position;
        panelReconstruccion.transform.rotation = cursorGuia.transform.rotation;

        // 2. Bloquear todo
        esperandoParaColocar = false;
        cursorGuia.SetActive(false);
        planeManager.enabled = false; // Ya no necesitamos buscar más pisos
        OcultarPlanos(); // Limpieza visual
        
        textoInstrucciones.text = "Traducción completada";
    }

    void OcultarPlanos()
    {
        foreach (var plano in planeManager.trackables)
        {
            plano.gameObject.SetActive(false);
        }
    }
}