using UnityEngine;
using TMPro;

public class ControladorLista : MonoBehaviour
{
    [Header("Configuración")]
    public Transform contenedorPadre; // Arrastra aquí 'ContenedorTextos'
    public GameObject prefabTexto;    // Arrastra aquí tu prefab de la carpeta

    // Llama a esta función para borrar lo anterior (útil al re-escanear)
    public void LimpiarHoja()
    {
        foreach (Transform hijo in contenedorPadre)
        {
            Destroy(hijo.gameObject);
        }
    }

    // Llama a esta función para añadir una línea nueva
    public void AgregarNuevoTexto(string mensaje)
    {
        // 1. Instanciar (Crear copia)
        GameObject nuevaCopia = Instantiate(prefabTexto, contenedorPadre);

        // 2. Escribir el texto
        TMP_Text textoComponente = nuevaCopia.GetComponent<TMP_Text>();
        if (textoComponente != null)
        {
            textoComponente.text = mensaje;
        }

        // 3. Resetear transformaciones (Evita errores visuales y de posición)
        nuevaCopia.transform.localScale = Vector3.one;
        
        Vector3 pos = nuevaCopia.transform.localPosition;
        pos.z = 0; // Pegarlo al papel en profundidad
        nuevaCopia.transform.localPosition = pos;
    }
}