import os, sys, time, torch
from diffusers import DiffusionPipeline, LCMScheduler
parca, toplam = int(sys.argv[1]), int(sys.argv[2])
satirlar = [l.strip().split("|", 1) for l in open("tasarim/promptlar.txt", encoding="utf-8") if "|" in l]
# sadece henüz sprite'ı olmayanlar çizilir (gece eklenenler)
eksik = [(i, a, p) for i, (a, p) in enumerate(satirlar) if not os.path.exists(f"sprite/{a}.png")]
benim = [x for j, x in enumerate(eksik) if j % toplam == parca]
if not benim:
    print("bu parçaya düşen yeni görsel yok"); sys.exit(0)
torch.set_num_threads(os.cpu_count())
t0 = time.time()
pipe = DiffusionPipeline.from_pretrained("stabilityai/stable-diffusion-xl-base-1.0", variant="fp16", torch_dtype=torch.float32)
pipe.scheduler = LCMScheduler.from_config(pipe.scheduler.config)
pipe.load_lora_weights("latent-consistency/lcm-lora-sdxl", adapter_name="lcm")
pipe.load_lora_weights("nerijs/pixel-art-xl", weight_name="pixel-art-xl.safetensors", adapter_name="pixel")
pipe.set_adapters(["lcm", "pixel"], adapter_weights=[1.0, 1.2])
print(f"model hazir {time.time()-t0:.0f}s, {len(benim)} gorsel", flush=True)
os.makedirs("ham", exist_ok=True)
neg = "3d render, realistic photo, blurry, multiple characters, text, watermark, frame, cropped"
for i, ad, prompt in benim:
    t = time.time()
    img = pipe(prompt=prompt, negative_prompt=neg, num_inference_steps=8, guidance_scale=1.5,
               width=768, height=768, generator=torch.Generator().manual_seed(1000 + i)).images[0]
    img.save(f"ham/{ad}.png")
    print(f"OK {ad} {time.time()-t:.0f}s", flush=True)
