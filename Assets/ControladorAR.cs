using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections;
using System.Collections.Generic; // Necesario para Listas
using TMPro;

public class ControladorAR : MonoBehaviour
{
    [Header("AR Components")]
    public ARPlaneManager planeManager;
    public ARRaycastManager raycastManager;
    public GameObject cursorGuia;
    public GameObject panelReconstruccion;

    [Header("Referencias Externas")]
    public ControladorOCR controladorOCR;

    [Header("UI")]
    public TextMeshProUGUI textoInstrucciones; 

    // Estados
    private bool buscandoSuelo = false;
    private Pose ultimaPosicionValida; // Guardamos dónde ponerlo
    private bool posicionEncontrada = false;

    void Start()
    {
        // 1. Apagar todo al inicio
        planeManager.enabled = false; 
        cursorGuia.SetActive(false);
        panelReconstruccion.SetActive(false); // El documento empieza oculto
        
        // 2. Iniciar secuencia
        StartCoroutine(IniciarSecuenciaAutomatica());
    }

    IEnumerator IniciarSecuenciaAutomatica()
    {
        if(textoInstrucciones) textoInstrucciones.text = "Iniciando cámara...";
        yield return new WaitForSeconds(1.0f);
        
        if(controladorOCR != null)
        {
            controladorOCR.IniciarEscaneo();
        }
    }

    // Este método lo llama el ControladorOCR cuando vuelve de la cámara
    public void NotificarRegresoDeCamara()
    {
        planeManager.enabled = true; // Encendemos el cerebro AR
        buscandoSuelo = true;
        
        if(textoInstrucciones) textoInstrucciones.text = "Mueve el celular lentamente para detectar el suelo...";
        
        // Aseguramos que el panel siga oculto hasta que el usuario decida
        panelReconstruccion.SetActive(false);
    }

    void Update()
    {
        if (!buscandoSuelo) return;

        ActualizarCursor();
        DetectarToque();
    }

    void ActualizarCursor()
    {
        var centro = new Vector2(Screen.width / 2, Screen.height / 2);
        var hits = new List<ARRaycastHit>();

        // Intentamos detectar planos o puntos
        if (raycastManager.Raycast(centro, hits, TrackableType.PlaneWithinPolygon | TrackableType.FeaturePoint))
        {
            cursorGuia.SetActive(true);
            
            // Guardamos la posición
            Pose poseDetectada = hits[0].pose;
            cursorGuia.transform.position = poseDetectada.position;

            // --- CORRECCIÓN DE ROTACIÓN (El truco) ---
            // Ignoramos la rotación que nos da Unity y forzamos que mire "hacia arriba" (plano)
            // Esto evita que salga vertical.
            // Si tu cursor sale de lado con esto, cambia Vector3.up por Vector3.forward
            cursorGuia.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up); 
            
            // Corrección extra: Si es un plano plano (Plane), usamos su rotación real si queremos
            if(hits[0].trackable is ARPlane)
            {
                cursorGuia.transform.rotation = hits[0].pose.rotation;
            }

            ultimaPosicionValida = hits[0].pose;
            posicionEncontrada = true;

            if(textoInstrucciones) textoInstrucciones.text = "¡Superficie detectada! Toca para colocar";
        }
        else
        {
            cursorGuia.SetActive(false);
            posicionEncontrada = false;
            if(textoInstrucciones) textoInstrucciones.text = "Buscando superficie...";
        }
    }

    void DetectarToque()
    {
        // Soporte para dedo (Touch) o Mouse (Click en editor)
        bool tocar = (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) || Input.GetMouseButtonDown(0);

        if (tocar && posicionEncontrada)
        {
            ColocarDocumentoFinal();
        }
    }

    void ColocarDocumentoFinal()
    {
        Debug.Log("COLOCANDO DOCUMENTO...");

        // 1. Activar el documento en la posición del cursor
        panelReconstruccion.SetActive(true);
        panelReconstruccion.transform.position = cursorGuia.transform.position;
        
        // Forzamos rotación plana para que se lea bien en el suelo/mesa
        // (90 grados en X suele ser "acostado" para UI en World Space)
        panelReconstruccion.transform.rotation = Quaternion.Euler(90, 0, 0);
        
        // 2. IMPORTANTE: Asegurar escala (a veces sale diminuto o gigante)
        panelReconstruccion.transform.localScale = Vector3.one; 

        // 3. APAGAR EL SISTEMA AR (Congelar todo)
        buscandoSuelo = false;       // Dejar de ejecutar Update
        cursorGuia.SetActive(false); // Ocultar cursor
        planeManager.enabled = false;// Dejar de buscar planos (ahorra batería)
        OcultarPlanosExistentes();   // Limpiar lo visual

        if(textoInstrucciones) textoInstrucciones.text = "Documento Anclado";
    }

    void OcultarPlanosExistentes()
    {
        foreach (var plane in planeManager.trackables)
        {
            plane.gameObject.SetActive(false);
        }
    }
}