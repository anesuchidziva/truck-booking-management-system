import os
from typing import List
import httpx
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field

app = FastAPI(title="Amcotts Diagnostic Service", version="0.1.0")

class DiagnosisRequest(BaseModel):
    # Empty evidence is a valid fallback case: explain that manual review is
    # required instead of rejecting the request outright.
    symptoms: List[str] = Field(min_length=0, max_length=12)
    observations: List[str] = Field(default_factory=list, max_length=12)
    complaint: str | None = Field(default=None, max_length=500)
    vehicle_make: str | None = Field(default=None, max_length=80)

class Suggestion(BaseModel):
    rank: int
    fault_category_id: str
    label: str
    action: str
    score: float
    score_type: str = "evidence_score_not_probability"
    evidence: List[str]

class DiagnosisResponse(BaseModel):
    result: str
    method: str
    model_version: str
    suggestions: List[Suggestion]
    message: str | None = None

RULES = {
    "fuel": ("fuel-system-restriction", "Fuel system restriction", "Inspect fuel filters and injector return flow", {"loss of power", "power loss", "black smoke", "rough idle", "hard starting"}),
    "cooling": ("cooling-system-leak", "Cooling system leak", "Pressure-test cooling system and inspect hoses", {"overheating", "coolant loss", "cooling issue"}),
    "brake": ("brake-system-pressure", "Brake system pressure fault", "Inspect air pressure, lines and brake actuator", {"brake warning", "brake squeal", "abs light", "soft pedal"}),
    "lubrication": ("lubrication-pressure", "Lubrication pressure fault", "Check oil level, sensor and pump pressure", {"oil pressure warning", "oil leak"}),
}

HF_TOKEN = os.getenv("HF_TOKEN") or os.getenv("HUGGINGFACEHUB_API_TOKEN")
HF_MODEL = os.getenv("HF_MODEL", "facebook/bart-large-mnli")
HF_INFERENCE_URL = os.getenv("HF_INFERENCE_URL", "")

def local_baseline(observed: set[str]):
    ranked=[]
    for key,(fid,label,action,evidence) in RULES.items():
        overlap = sorted(observed & evidence)
        if overlap: ranked.append((len(overlap), fid,label,action,overlap))
    ranked.sort(key=lambda x:(-x[0], x[1]))
    return ranked

def huggingface_suggestions(text: str, observed: set[str]):
    if not HF_TOKEN or not HF_INFERENCE_URL:
        return None
    labels = [rule[1] for rule in RULES.values()]
    response = httpx.post(
        HF_INFERENCE_URL,
        headers={"Authorization": f"Bearer {HF_TOKEN}"},
        json={"inputs": text, "parameters": {"candidate_labels": labels, "multi_label": True}},
        timeout=12.0,
    )
    response.raise_for_status()
    payload = response.json()
    if not isinstance(payload, dict) or not isinstance(payload.get("labels"), list):
        return None
    by_label = {rule[1]: rule for rule in RULES.values()}
    ranked = []
    for label, score in zip(payload.get("labels", []), payload.get("scores", [])):
        rule = by_label.get(label)
        if rule and float(score) >= 0.35:
            ranked.append((float(score), rule[0], rule[1], rule[2], sorted(observed)))
    return ranked[:3]

@app.get("/health")
def health(): return {"status": "ok", "model_version": HF_MODEL if HF_TOKEN and HF_INFERENCE_URL else "local-evidence-baseline", "provider": "huggingface" if HF_TOKEN and HF_INFERENCE_URL else "local"}

@app.post("/diagnose", response_model=DiagnosisResponse)
def diagnose(request: DiagnosisRequest):
    observed = set(s.lower().strip() for s in request.symptoms + request.observations)
    text = ". ".join(request.symptoms + request.observations + ([request.complaint] if request.complaint else []))
    method = "local_evidence_baseline"
    model_version = "local-evidence-0.1"
    try:
        ranked = huggingface_suggestions(text, observed)
        if ranked is not None:
            method = "huggingface_zero_shot"
            model_version = HF_MODEL
    except (httpx.HTTPError, ValueError):
        ranked = None
    if ranked is None:
        ranked = local_baseline(observed)
    if not ranked:
        return DiagnosisResponse(result="manual_review_required", method=method, model_version=model_version, suggestions=[], message="Insufficient supported evidence; manual inspection is required.")
    suggestions=[Suggestion(rank=i+1,fault_category_id=x[1],label=x[2],action=x[3],score=round(float(x[0]) if method == "huggingface_zero_shot" else x[0]/max(1,len(observed)),3),evidence=x[4]) for i,x in enumerate(ranked[:3])]
    return DiagnosisResponse(result="suggestions_available",method=method,model_version=model_version,suggestions=suggestions,message="Suggestions require mechanic verification and are not confirmed faults.")
