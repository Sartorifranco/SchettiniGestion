# -*- coding: utf-8 -*-
"""Genera el PDF del informe de mejoras SCHPOS para el cliente."""
import os
from fpdf import FPDF

OUTPUT = os.path.join(os.path.dirname(__file__), "Informe_Mejoras_SCHPOS.pdf")
FONT_REG = r"C:\Windows\Fonts\arial.ttf"
FONT_BOLD = r"C:\Windows\Fonts\arialbd.ttf"


class InformePdf(FPDF):
    def footer(self):
        self.set_y(-15)
        self.set_font("Arial", "", 9)
        self.set_text_color(120, 120, 120)
        self.cell(0, 10, f"Página {self.page_no()}", align="C")


def body(pdf, text):
    pdf.set_x(pdf.l_margin)
    pdf.set_font("Arial", "", 11)
    pdf.multi_cell(pdf.epw, 6, text)
    pdf.ln(1)


def section_title(pdf, text):
    pdf.ln(4)
    pdf.set_x(pdf.l_margin)
    pdf.set_font("Arial", "B", 14)
    pdf.set_text_color(30, 136, 229)
    pdf.multi_cell(pdf.epw, 8, text)
    pdf.set_text_color(0, 0, 0)
    pdf.ln(2)


def subsection(pdf, text):
    pdf.ln(2)
    pdf.set_x(pdf.l_margin)
    pdf.set_font("Arial", "B", 12)
    pdf.multi_cell(pdf.epw, 7, text)
    pdf.ln(1)


def bullet(pdf, text):
    pdf.set_x(pdf.l_margin)
    pdf.set_font("Arial", "", 11)
    pdf.multi_cell(pdf.epw, 6, f"- {text}")


def main():
    pdf = InformePdf()
    pdf.set_auto_page_break(auto=True, margin=20)
    pdf.add_font("Arial", "", FONT_REG)
    pdf.add_font("Arial", "B", FONT_BOLD)

    # Portada
    pdf.add_page()
    pdf.set_margins(20, 20, 20)
    pdf.ln(30)
    pdf.set_font("Arial", "B", 22)
    pdf.cell(0, 12, "Informe de Mejoras", align="C", new_x="LMARGIN", new_y="NEXT")
    pdf.set_font("Arial", "B", 18)
    pdf.cell(0, 10, "Schettini Gestión (SCHPOS)", align="C", new_x="LMARGIN", new_y="NEXT")
    pdf.ln(8)
    pdf.set_font("Arial", "", 12)
    pdf.set_text_color(80, 80, 80)
    pdf.set_x(pdf.l_margin)
    pdf.multi_cell(pdf.epw, 7, "Documento para el cliente\nDesde integración ARCA hasta últimas actualizaciones", align="C")
    pdf.set_text_color(0, 0, 0)
    pdf.ln(20)
    pdf.set_x(pdf.l_margin)
    pdf.set_font("Arial", "", 11)
    pdf.multi_cell(pdf.epw, 6, "Este informe resume las mejoras incorporadas al sistema de gestión y facturación, en lenguaje claro y sin tecnicismos innecesarios.")

    # Parte 1
    pdf.add_page()
    pdf.set_x(pdf.l_margin)
    pdf.set_font("Arial", "B", 18)
    pdf.multi_cell(pdf.epw, 10, "Parte 1 — Trabajos realizados en el sistema")
    pdf.ln(4)

    section_title(pdf, "1. Facturación electrónica con ARCA")
    body(pdf, "Se incorporó en Configuración → Negocio y ARCA una sección llamada «Activación Fiscal ARCA», para preparar el comercio para facturar legalmente.")
    body(pdf, "Qué puede hacer el usuario desde el sistema:")
    bullet(pdf, "Completar datos del negocio (CUIT, razón social, nombre de fantasía).")
    bullet(pdf, "Generar el pedido de certificado (CSR) con un botón, usando esos datos.")
    bullet(pdf, "Subir el certificado (.crt) que devuelve ARCA después del trámite web.")
    bullet(pdf, "Probar la conexión con ARCA antes de facturar en serio.")
    bullet(pdf, "Elegir modo prueba (homologación) o modo producción (facturación real).")
    body(pdf, "Importante: la clave privada se guarda de forma segura en la computadora donde se generó el pedido. El certificado (.crt) debe corresponder a ese mismo pedido y a esa misma PC. También se mantiene la opción de usar un certificado .pfx existente.")

    section_title(pdf, "2. Corrección del «Costo final» de productos")
    body(pdf, "Se corrigió un error cuando el producto tenía marcado «El costo incluye IVA». Antes el sistema mostraba un costo final más bajo de lo esperado. Ahora, si el costo ingresado ya incluye IVA, el costo final respeta ese valor (más impuesto interno si corresponde).")

    section_title(pdf, "3. Pantalla del cliente (segundo monitor)")
    body(pdf, "Se mejoró la pantalla que ve el cliente en el mostrador:")
    bullet(pdf, "Carrusel de imágenes y videos publicitarios.")
    bullet(pdf, "Carpeta configurable para subir archivos (JPG, PNG, GIF, MP4, AVI).")
    bullet(pdf, "Recarga de publicidades sin cerrar la pantalla.")
    bullet(pdf, "Botón «Vista previa» en Configuración.")

    section_title(pdf, "4. Listas de precios — recálculo automático")
    body(pdf, "Al cambiar el costo de compra (recepción de mercadería o actualización manual), el sistema recalcula automáticamente los precios según las listas asignadas (Mayorista, Minorista, etc.). Las listas de tipo «Precio fijo» siguen siendo manuales.")

    section_title(pdf, "5. Mejoras en la ficha de producto (pestaña Precios)")
    bullet(pdf, "Se eliminó «Cobrar IVA al cliente» para evitar doble cálculo.")
    bullet(pdf, "Grilla con todas las listas: costo base, tipo, regla y precio final.")
    bullet(pdf, "Actualización instantánea al cambiar costo, IVA o impuesto interno.")
    bullet(pdf, "El % de IVA se movió a «Datos principales», junto al costo.")
    bullet(pdf, "Al crear un producto nuevo, todas las listas quedan asignadas por defecto.")

    section_title(pdf, "6. Exportación e importación masiva de productos")
    body(pdf, "En el módulo Productos:")
    subsection(pdf, "Exportar productos")
    body(pdf, "Genera Excel o CSV con: identificador, código, nombre, costo, % IVA, impuesto interno, Costo incluye IVA (SI/NO), Es stockeable (SI/NO), Vende en negativo (SI/NO).")
    subsection(pdf, "Importar actualización")
    body(pdf, "Sube el archivo modificado. Identifica por ProductoID (o código). Aplica cambios y recalcula precios. Si falla una fila, no guarda nada y avisa el error.")
    subsection(pdf, "Significado de columnas SI/NO")
    bullet(pdf, "CostoIncluyeIva = SI: el costo ya trae IVA de compra. NO: el costo es neto y el sistema suma el % IVA.")
    bullet(pdf, "EsStockeable = SI: controla stock. NO: no controla stock (servicios).")
    bullet(pdf, "VendeEnNegativo = SI: permite vender con stock negativo. NO: exige stock suficiente.")
    body(pdf, "Celda vacía = ese dato no se modifica.")

    section_title(pdf, "7. Ventana de resultado de importación")
    body(pdf, "Se reemplazó el cuadro gris de Windows por una ventana propia con tema oscuro del sistema.")

    section_title(pdf, "8. Corrección técnica menor")
    body(pdf, "Corrección interna en la pantalla del cliente para estabilidad del desarrollo.")

    # Parte 2 ARCA
    pdf.add_page()
    pdf.set_x(pdf.l_margin)
    pdf.set_font("Arial", "B", 18)
    pdf.multi_cell(pdf.epw, 10, "Parte 2 — Guía paso a paso: trámite ARCA")
    pdf.ln(2)
    body(pdf, "Guía orientativa. Los nombres en el portal ARCA pueden variar; la lógica del trámite es la misma.")

    subsection(pdf, "Requisitos previos")
    bullet(pdf, "CUIT activo del negocio.")
    bullet(pdf, "Clave Fiscal nivel 3 o superior.")
    bullet(pdf, "Punto de venta dado de alta para facturación electrónica.")
    bullet(pdf, "Servicios habilitados en ARCA: Facturación electrónica y Administración de certificados digitales.")

    subsection(pdf, "Paso 1 — Completar datos en SCHPOS")
    body(pdf, "Configuración → Negocio y ARCA. Completar CUIT (11 dígitos), razón social, nombre de fantasía, dirección y punto de venta. Guardar.")

    subsection(pdf, "Paso 2 — Generar el pedido de certificado")
    body(pdf, "Pulsar «Generar Pedido de Certificado (CSR)». Guardar el archivo .csr. Hacerlo en la misma PC que usarán para facturar.")

    subsection(pdf, "Paso 3 — Subir el pedido en el portal ARCA")
    body(pdf, "Entrar a arca.gob.ar con Clave Fiscal → Administrador de certificados digitales → subir el .csr → descargar el .crt emitido.")

    subsection(pdf, "Paso 4 — Importar el certificado en SCHPOS")
    body(pdf, "Configuración → Subir .crt → elegir archivo de ARCA → Guardar.")

    subsection(pdf, "Paso 5 — Probar en modo PRUEBA")
    body(pdf, "Dejar desmarcado «Ambiente ARCA: producción». Probar conexión. Emitir facturas de prueba (sin validez fiscal real).")

    subsection(pdf, "Paso 6 — Pasar a facturación REAL")
    body(pdf, "Marcar «Ambiente ARCA: producción». Guardar. Probar conexión. Emitir factura real de monto bajo. Conviene hacerlo con el contador la primera vez.")

    subsection(pdf, "Alternativa: certificado .pfx")
    body(pdf, "Si el contador entrega un .pfx con contraseña, cargarlo en la sección correspondiente y seguir pasos 5 y 6.")

    subsection(pdf, "Resumen del flujo")
    body(pdf, "Datos del negocio → Generar CSR → Subir CSR en ARCA → Descargar .crt → Subir .crt en SCHPOS → Probar en modo prueba → Facturas de prueba OK → Activar producción → Facturación real.")

    subsection(pdf, "Recomendaciones finales")
    bullet(pdf, "Siempre probar en homologación antes de producción.")
    bullet(pdf, "Renovar el certificado antes de que venza.")
    bullet(pdf, "No copiar certificados a otra PC sin repetir el trámite CSR en esa máquina.")
    bullet(pdf, "Para actualizar precios masivamente: Exportar → editar Excel → Importar actualización.")
    bullet(pdf, "Ante dudas fiscales, consultar al contador.")

    pdf.output(OUTPUT)
    print(f"PDF generado: {OUTPUT}")


if __name__ == "__main__":
    main()
