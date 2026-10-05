import base64
import json
import re
import time
import urllib.error
import urllib.request

TOKEN = "token-de-servico-de-teste-com-mais-de-32-bytes"


def postar(url, corpo):
    pedido = urllib.request.Request(
        url,
        json.dumps(corpo).encode(),
        {"Content-Type": "application/json", "X-Prdal-Servico": TOKEN},
    )
    try:
        resposta = urllib.request.urlopen(pedido, timeout=30)
    except urllib.error.HTTPError as erro:
        resposta = erro
    with resposta:
        return resposta.status, resposta.headers, resposta.read()


def invocar(rota, markdown):
    evento = {
        "version": "2.0",
        "rawPath": rota,
        "headers": {"content-type": "application/json", "X-Prdal-Servico": TOKEN},
        "requestContext": {
            "http": {"method": "POST", "path": rota, "sourceIp": "127.0.0.1"}
        },
        "body": json.dumps({"markdown": markdown}),
        "isBase64Encoded": False,
    }
    status, _, corpo = postar(
        "http://127.0.0.1:18085/2015-03-31/functions/function/invocations", evento
    )
    assert status == 200, corpo[:1000]
    resposta = json.loads(corpo)
    assert "statusCode" in resposta, resposta
    return resposta


for linhas in (1, 300):
    markdown = "# Nome\n\n" + "- Desenvolvi integracoes em C# e SQL\n" * linhas
    status, headers, pdf = postar(
        "http://127.0.0.1:18084/render/pdf", {"markdown": markdown}
    )
    assert status == 200, pdf[:1000]
    paginas = int(headers["X-Paginas"])
    assert paginas == len(re.findall(rb"/Type\s*/Page\b", pdf))
    assert paginas == 1 if linhas == 1 else paginas > 1
    inicio = time.monotonic()
    resposta = invocar("/render/pdf", markdown)
    duracao = time.monotonic() - inicio
    assert resposta["statusCode"] == 200, resposta
    assert resposta["isBase64Encoded"]
    pdf = base64.b64decode(resposta["body"])
    paginas = int(next(v for k, v in resposta["headers"].items() if k.lower() == "x-paginas"))
    assert paginas == len(re.findall(rb"/Type\s*/Page\b", pdf))
    assert paginas == 1 if linhas == 1 else paginas > 1
    print(f"PDF HTTP/Lambda: {paginas} pagina(s); invocacao local {duracao:.3f}s")

status, _, docx = postar("http://127.0.0.1:18084/render/docx", {"markdown": "# Nome"})
assert status == 200 and docx.startswith(b"PK")
resposta = invocar("/render/docx", "# Nome")
assert resposta["statusCode"] == 200 and base64.b64decode(resposta["body"]).startswith(b"PK")

for rota in ("/render/pdf", "/render/docx"):
    markdown = "x" * (8 * 1024 * 1024)
    status, _, corpo = postar(f"http://127.0.0.1:18084{rota}", {"markdown": markdown})
    assert status == 413, corpo[:1000]
    resposta = invocar(rota, markdown)
    assert resposta["statusCode"] == 413, resposta
    print(f"8 MB rejeitados em HTTP/Lambda: {rota}")
