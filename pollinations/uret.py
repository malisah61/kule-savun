import os, time, urllib.parse, urllib.request
os.makedirs('pollinations/cikti', exist_ok=True)
lines = [l.strip().split('|', 1) for l in open('pollinations/prompts.txt', encoding='utf-8') if '|' in l]
for i, (name, prompt) in enumerate(lines):
    out = f'pollinations/cikti/{name}.jpg'
    url = 'https://image.pollinations.ai/prompt/' + urllib.parse.quote(prompt) + '?width=768&height=768&seed=61&nologo=true'
    for attempt in range(4):
        try:
            req = urllib.request.Request(url, headers={'User-Agent': 'kule-savun-sprite-bot'})
            data = urllib.request.urlopen(req, timeout=180).read()
            if len(data) < 2000: raise RuntimeError('cok kucuk yanit')
            open(out, 'wb').write(data); print('OK', name, len(data)); break
        except Exception as e:
            print('HATA', name, attempt, e); time.sleep(20)
    time.sleep(17)
