from flask import Flask, request, jsonify
import numpy as np
import pandas as pd
from sklearn.linear_model import LinearRegression, LogisticRegression
from sklearn.preprocessing import LabelEncoder, StandardScaler
from sklearn.utils.class_weight import compute_class_weight

app = Flask(__name__) #creez aplicatia flask

engagement = pd.read_csv("engagement.csv")
performance = pd.read_csv("performance.csv")

data = pd.merge(engagement, performance, on='Student ID') #combin cele doua tabele dupa coloana studentID

le_engagement = LabelEncoder() 
data['Engagement Level'] = le_engagement.fit_transform(data['Engagement Level'])
print("Engagement mapping:", dict(zip(le_engagement.classes_, le_engagement.transform(le_engagement.classes_))))

le_class = LabelEncoder()
data['Class'] = le_class.fit_transform(data['Class'])
print("Class mapping:", dict(zip(le_class.classes_, le_class.transform(le_class.classes_))))

engagement_features = [
    '# Logins', '# Content Reads', 
    '# Quiz Reviews before submission', 
    'Assignment 1 lateness indicator',
    'Assignment 2 lateness indicator', 
    'Assignment 3 lateness indicator',
    'Assignment 1 duration to submit (in hours)', 
    'Assignment 2 duration to submit (in hours)',
    'Assignment 3 duration to submit (in hours)', 
    'Average time to submit assignment (in hours)',
    'Engagement Level'
]

performance_features = [
    'Quiz01 [10]', 
    'Assignment01 [8]',
    'Assignment02 [12]', 
    'Assignment03 [25]',
    'Midterm Exam [20]',
    'Final Exam [35]'
]

features = engagement_features + performance_features

print(f"Features ({len(features)}):")
for idx, feat in enumerate(features):
    print(f"{idx}: {feat}")

missing_cols = [col for col in features if col not in data.columns]
if missing_cols:
    raise Exception(f"Missing columns in data: {missing_cols}")

X = data[features]

target_grade = 'Course Grade'
target_dropout = 'Class'
target_progress = 'Total [100]'

y_grade = data[target_grade]
y_dropout = data[target_dropout]
y_progress = data[target_progress]

scaler = StandardScaler()
X_scaled = scaler.fit_transform(X)

classes = np.unique(y_dropout)
class_weights = compute_class_weight('balanced', classes=classes, y=y_dropout)
class_weight_dict = dict(zip(classes, class_weights))

lin_reg_grade = LinearRegression()
lin_reg_grade.fit(X_scaled, y_grade)

log_reg = LogisticRegression(max_iter=1000, random_state=42, class_weight=class_weight_dict)
log_reg.fit(X_scaled, y_dropout)

module_grades = {
    'Module 1': ['Quiz01 [10]', 'Assignment01 [8]'],
    'Module 2': ['Assignment02 [12]'],
    'Module 3': ['Assignment03 [25]'],
    'Module 4': ['Midterm Exam [20]'],
    'Module 5': ['Final Exam [35]']
}

module_models = {}
for module_name, module_features in module_grades.items():

    module_feature_indices = [features.index(f) for f in module_features if f in features]
    X_module = X_scaled[:, module_feature_indices]
    y_module = data[module_features].mean(axis=1)

    model = LinearRegression()
    model.fit(X_module, y_module)
    module_models[module_name] = {
        'model': model,
        'feature_indices': module_feature_indices,
        'feature_names': module_features
    }

print("\nExample predictions on training data:")
print("Grade:", lin_reg_grade.predict(X_scaled)[:5])

print("Dropout probabilities:", log_reg.predict_proba(X_scaled)[:5])
print("Dropout predictions:", log_reg.predict(X_scaled)[:5])

def predict(features_list):
    print(f"\n[DEBUG] Received features_list ({len(features_list)}): {features_list}")

    if len(features_list) != len(features):
        raise ValueError(f"Input must have {len(features)} features, got {len(features_list)}.")

    engagement_idx = features.index("Engagement Level")
    engagement_value = features_list[engagement_idx]

    print(f"[DEBUG] Original Engagement Level value: {engagement_value}")

    if isinstance(engagement_value, str):
        try:
            engagement_encoded = le_engagement.transform([engagement_value])[0]
            print(f"[DEBUG] Encoded Engagement Level: {engagement_encoded}")
        except ValueError:
            raise ValueError(f"Unknown Engagement Level value: {engagement_value}")
        features_list[engagement_idx] = engagement_encoded
    else:
        print(f"[DEBUG] Engagement Level already encoded as: {engagement_value}")

    x_input = pd.DataFrame([features_list], columns=features)
    print(f"[DEBUG] Input DataFrame for scaler:\n{x_input}")

    x_scaled = scaler.transform(x_input)
    print(f"[DEBUG] Scaled features:\n{x_scaled}")

    pred_grade = lin_reg_grade.predict(x_scaled)[0]
   
    dropout_proba = log_reg.predict_proba(x_scaled)[0]
    pred_dropout = log_reg.predict(x_scaled)[0]

    print(f"[DEBUG] Predicted Grade (before clamp): {pred_grade}")

    print(f"[DEBUG] Dropout probabilities: {dropout_proba}")
    print(f"[DEBUG] Predicted Dropout: {pred_dropout}")

    module_predictions = {}
    for module_name, module_data in module_models.items():
        x_module = x_scaled[:, module_data['feature_indices']]
        pred = module_data['model'].predict(x_module)[0]
        pred_clamped = max(0.0, min(float(pred), 100.0))
        module_predictions[module_name] = pred_clamped
        print(f"[DEBUG] Module '{module_name}' prediction (clamped): {pred_clamped}")

    pred_grade = max(0.0, min(pred_grade, 100.0))


    print(f"[DEBUG] Final predicted Grade (clamped): {pred_grade}")


    return {
        "predicted_grade": float(pred_grade),
        "predicted_dropout": int(pred_dropout),
        "dropout_probability": float(dropout_proba[1]),  
        "module_predictions": module_predictions
    }

@app.route('/predict', methods=['POST'])
def predict_endpoint():
    data = request.json
    print(f"[DEBUG] Received JSON data: {data}")

    features_list = data.get('features')
    if features_list is None or not isinstance(features_list, list):
        return jsonify({"error": "Invalid input. 'features' must be a list."}), 400

    try:
        prediction = predict(features_list)
        print(f"[DEBUG] Prediction result: {prediction}")
        return jsonify(prediction)
    except Exception as e:
        print("Prediction error:", str(e))
        return jsonify({"error": str(e)}), 500


@app.route('/encodings', methods=['GET'])
def encodings_endpoint():
    engagement_mapping = {
        class_name: int(label)
        for class_name, label in zip(le_engagement.classes_, range(len(le_engagement.classes_)))
    }

    class_mapping = {
        class_name: int(label)
        for class_name, label in zip(le_class.classes_, range(len(le_class.classes_)))
    }

    return jsonify({
        "Engagement Level": engagement_mapping,
        "Class": class_mapping
    })

@app.route('/features', methods=['GET'])
def features_endpoint():
    return jsonify({
        "features_order": features,
        "features_description": {
            "# Logins": "Total number of system logins",
            "# Content Reads": "Number of content pages viewed",
            "# Quiz Reviews before submission": "Number of quiz reviews before final submission",
            "Assignment 1/2/3 lateness indicator": "Binary (0=on time, 1=late)",
            "Assignment 1/2/3 duration to submit (in hours)": "Time taken to submit assignment",
            "Average time to submit assignment (in hours)": "Average across all assignments",
            "Engagement Level": "H (High) or L (Low)",
            "Quiz01 [10]": "Score for Quiz 1 (out of 10)",
            "Assignment01 [8]": "Score for Assignment 1 (out of 8)",
            "Assignment02 [12]": "Score for Assignment 2 (out of 12)",
            "Assignment03 [25]": "Score for Assignment 3 (out of 25)",
            "Midterm Exam [20]": "Score for Midterm Exam (out of 20)",
            "Final Exam [35]": "Score for Final Exam (out of 35)"
        }
    })

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=8000, debug=True)