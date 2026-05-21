import os
import re

legacy_dir = r"D:\Project_ORORA\Assets\unity-chan!\Unity-chan! Model\Art\UnityChanShader\Shader"
out_dir = r"D:\Project_ORORA\Assets\KJ_Work\Shaders\UnityChanURP"
if not os.path.exists(out_dir):
    os.makedirs(out_dir)

files = [f for f in os.listdir(legacy_dir) if f.endswith('.shader')]

for f in files:
    with open(os.path.join(legacy_dir, f), 'r', encoding='utf-8') as file:
        content = file.read()
    
    # 1. Parse Shader Name
    shader_name_match = re.search(r'Shader\s+"([^"]+)"', content)
    shader_name = shader_name_match.group(1) if shader_name_match else f"URP/{f}"
    new_shader_name = shader_name.replace("UnityChan/", "URP/UnityChan/")
    
    # 2. Parse Properties block
    properties_match = re.search(r'Properties\s*\{([\s\S]*?)\}\s*SubShader', content)
    properties_block = properties_match.group(1) if properties_match else ""
    
    # Extract property variables for CBUFFER
    cbuffer_vars = []
    for line in properties_block.split('\n'):
        m = re.search(r'([_A-Za-z0-9]+)\s*\("', line)
        if m:
            var_name = m.group(1)
            if "Tex" in var_name or "Sampler" in var_name or "Map" in var_name:
                cbuffer_vars.append(f"float4 {var_name}_ST;")
            elif "Color" in var_name:
                cbuffer_vars.append(f"half4 {var_name};")
            else:
                cbuffer_vars.append(f"float {var_name};")

    cbuffer_str = "CBUFFER_START(UnityPerMaterial)\n        " + "\n        ".join(cbuffer_vars) + "\n        CBUFFER_END"
    
    # 3. Parse SubShader Tags
    tags_match = re.search(r'Tags\s*\{([\s\S]*?)\}', content)
    tags_block = tags_match.group(1) if tags_match else '"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"'
    tags_block = tags_block.replace('"LightMode"="ForwardBase"', '')
    
    # Check global subshader states first
    subshader_match = re.search(r'SubShader\s*\{([\s\S]*?)\s*Pass\s*\{', content)
    global_states = subshader_match.group(1) if subshader_match else ""
    global_blend_match = re.search(r'(Blend\s+[^\n]+)', global_states)
    global_zwrite_match = re.search(r'(ZWrite\s+Off|ZWrite\s+On)', global_states)

    # 4. Parse Passes
    # We will split by Pass { ... }
    passes = re.findall(r'Pass\s*\{([\s\S]*?)\}(?=\s*Pass|\s*\}|\s*FallBack)', content)
    
    new_passes_str = ""
    for idx, p in enumerate(passes):
        # Extract states
        cull_match = re.search(r'(Cull\s+[a-zA-Z]+)', p)
        ztest_match = re.search(r'(ZTest\s+[a-zA-Z]+)', p)
        zwrite_match = re.search(r'(ZWrite\s+[a-zA-Z]+)', p)
        blend_match = re.search(r'(Blend\s+[^\n]+)', p)
        colormask_match = re.search(r'(ColorMask\s+[^\n]+)', p)
        
        cull = cull_match.group(1) if cull_match else ""
        ztest = ztest_match.group(1) if ztest_match else ""
        zwrite = zwrite_match.group(1) if zwrite_match else (global_zwrite_match.group(1) if global_zwrite_match else "")
        blend = blend_match.group(1) if blend_match else (global_blend_match.group(1) if global_blend_match else "")
        colormask = colormask_match.group(1) if colormask_match else ""
        
        # Check which include it uses
        if 'CharaMain.cg' in p:
            include_line = '#include "KJ_CharaMain_URP.hlsl"'
            pass_name = 'Name "ForwardLit"'
            light_mode = '"LightMode"="UniversalForward"'
        elif 'CharaSkin.cg' in p:
            include_line = '#include "KJ_CharaSkin_URP.hlsl"'
            pass_name = 'Name "ForwardLit"'
            light_mode = '"LightMode"="UniversalForward"'
        elif 'CharaOutline.cg' in p:
            include_line = '#include "KJ_CharaOutline_URP.hlsl"'
            pass_name = 'Name "Outline"'
            light_mode = '"LightMode"="SRPDefaultUnlit"'
            # In URP outline passes usually use SRPDefaultUnlit or standard Unlit
        else:
            continue
            
        render_states = f"{cull}\n            {ztest}\n            {zwrite}\n            {blend}\n            {colormask}".strip()
        
        new_pass = f"""
        Pass
        {{
            {pass_name}
            Tags {{ {light_mode} }}
            
            {render_states}

            HLSLPROGRAM
            #pragma target 2.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            {cbuffer_str}

            {include_line}
            ENDHLSL
        }}
"""
        new_passes_str += new_pass
        
    shadowcaster_str = """
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
"""
        
    new_shader_content = f"""Shader "{new_shader_name}"
{{
    Properties
    {{{properties_block}}}
    SubShader
    {{
        Tags
        {{
            {tags_block.strip()}
            "RenderPipeline"="UniversalPipeline"
        }}
        {new_passes_str}
        {shadowcaster_str}
    }}
}}
"""
    # Write new file
    new_filename = f.replace(".shader", "_URP.shader")
    with open(os.path.join(out_dir, new_filename), 'w', encoding='utf-8') as out_f:
        out_f.write(new_shader_content)
    
    print(f"Converted {f} to {new_filename}")
