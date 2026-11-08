import os, time, subprocess, torch
from diffusers import DiffusionPipeline, LCMScheduler
torch.set_num_threads(os.cpu_count())
t0 = time.time()
pipe = DiffusionPipeline.from_pretrained("stabilityai/stable-diffusion-xl-base-1.0", variant="fp16", torch_dtype=torch.float32)
pipe.scheduler = LCMScheduler.from_config(pipe.scheduler.config)
pipe.load_lora_weights("latent-consistency/lcm-lora-sdxl", adapter_name="lcm")
pipe.load_lora_weights("nerijs/pixel-art-xl", weight_name="pixel-art-xl.safetensors", adapter_name="pixel")
pipe.set_adapters(["lcm", "pixel"], adapter_weights=[1.0, 1.2])
pipe.enable_attention_slicing()
print(f"model hazir {time.time()-t0:.0f}s", flush=True)
os.makedirs("sdxl/cikti", exist_ok=True)
neg = "3d render, realistic photo, blurry, multiple characters, text, watermark, frame"
for line in open("sdxl/prompts.txt", encoding="utf-8"):
    if "|" not in line: continue
    name, prompt = line.strip().split("|", 1)
    t = time.time()
    img = pipe(prompt=prompt, negative_prompt=neg, num_inference_steps=8, guidance_scale=1.5,
               width=768, height=768, generator=torch.Generator().manual_seed(61)).images[0]
    path = f"sdxl/cikti/{name}.png"; img.save(path)
    print(f"OK {name} {time.time()-t:.0f}s", flush=True)
    subprocess.run(["git", "add", path], check=True)
    subprocess.run(["git", "commit", "-qm", f"SDXL: {name}"], check=True)
    subprocess.run(["git", "push", "-q"], check=True)
