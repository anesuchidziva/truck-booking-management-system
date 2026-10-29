from app.main import DiagnosisRequest, diagnose

def client_diagnose(payload):
    return diagnose(DiagnosisRequest(**payload)).model_dump()

def test_returns_supported_suggestions():
    r=client_diagnose({'symptoms':['overheating','coolant loss']})
    assert r['suggestions'][0]['fault_category_id']=='cooling-system-leak'
def test_unsupported_is_manual_review():
    r=client_diagnose({'symptoms':['unlisted symptom']})
    assert r['result']=='manual_review_required' and r['suggestions']==[]

def test_ui_brake_symptoms_return_a_supported_suggestion():
    r=client_diagnose({'symptoms':['Brake Squeal','ABS Light']})
    assert r['suggestions'][0]['fault_category_id']=='brake-system-pressure'

def test_empty_evidence_returns_manual_review_fallback():
    r=client_diagnose({'symptoms':[]})
    assert r['result']=='manual_review_required'
    assert r['suggestions']==[]
