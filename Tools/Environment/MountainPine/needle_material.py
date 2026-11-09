"""Baked canopy-only needle-scale relief. No runtime nodes, opacity cards or new slots."""
def enhance_needles(mat):
 n=mat.node_tree.nodes;l=mat.node_tree.links;bs=n.get('Principled BSDF')
 base=bs.inputs['Base Color'].links[0].from_socket
 original_normal=bs.inputs['Normal'].links[0].from_socket if bs.inputs['Normal'].is_linked else None
 sep=n.new('ShaderNodeSeparateColor');sep.name='Canopy source color mask';l.new(base,sep.inputs[0])
 diff=n.new('ShaderNodeMath');diff.operation='SUBTRACT';l.new(sep.outputs[1],diff.inputs[0]);l.new(sep.outputs[0],diff.inputs[1])
 mask=n.new('ShaderNodeMapRange');mask.clamp=True;mask.inputs['From Min'].default_value=-.002;mask.inputs['From Max'].default_value=.018
 l.new(diff.outputs[0],mask.inputs[0])
 tc=n.new('ShaderNodeTexCoord')
 grains=[]
 for dims in ((65,8,65),(9,62,60)):
  mapping=n.new('ShaderNodeVectorMath');mapping.operation='MULTIPLY';mapping.inputs[1].default_value=dims;l.new(tc.outputs['Object'],mapping.inputs[0])
  noise=n.new('ShaderNodeTexVoronoi');noise.feature='DISTANCE_TO_EDGE';noise.inputs['Scale'].default_value=1.
  l.new(mapping.outputs[0],noise.inputs['Vector']);grains.append(noise.outputs['Distance'])
 direction=n.new('ShaderNodeTexNoise');direction.inputs['Scale'].default_value=4;l.new(tc.outputs['Object'],direction.inputs['Vector'])
 mixed=n.new('ShaderNodeMixRGB');l.new(direction.outputs['Fac'],mixed.inputs[0]);l.new(grains[0],mixed.inputs[1]);l.new(grains[1],mixed.inputs[2])
 sharpen=n.new('ShaderNodeValToRGB');sharpen.name='Fine elongated needle tuft relief';sharpen.color_ramp.elements[0].position=.025;sharpen.color_ramp.elements[1].position=.19;l.new(mixed.outputs[0],sharpen.inputs[0])
 strength=n.new('ShaderNodeMath');strength.operation='MULTIPLY';strength.inputs[1].default_value=.9;l.new(mask.outputs[0],strength.inputs[0])
 bump=n.new('ShaderNodeBump');bump.name='Canopy needle relief';bump.inputs['Distance'].default_value=.027;l.new(strength.outputs[0],bump.inputs['Strength']);l.new(sharpen.outputs[0],bump.inputs['Height'])
 if original_normal:l.new(original_normal,bump.inputs['Normal'])
 l.new(bump.outputs[0],bs.inputs['Normal'])
 # Small needle clusters also modulate the original green color, without recoloring the bark.
 cluster=n.new('ShaderNodeTexNoise');cluster.inputs['Scale'].default_value=7.;cluster.inputs['Detail'].default_value=3;l.new(tc.outputs['Object'],cluster.inputs['Vector'])
 cluster_ramp=n.new('ShaderNodeValToRGB');cluster_ramp.color_ramp.elements[0].color=(.38,.44,.31,1);cluster_ramp.color_ramp.elements[1].color=(1.02,1.03,.89,1)
 l.new(cluster.outputs['Fac'],cluster_ramp.inputs[0])
 multiply=n.new('ShaderNodeMixRGB');multiply.blend_type='MULTIPLY';l.new(mask.outputs[0],multiply.inputs[0]);l.new(base,multiply.inputs[1]);l.new(cluster_ramp.outputs[0],multiply.inputs[2]);l.new(multiply.outputs[0],bs.inputs['Base Color'])
 # Leaves remain matte in grazing light; the original bark roughness is preserved.
 rough=bs.inputs['Roughness'];old=rough.links[0].from_socket if rough.is_linked else None
 mix=n.new('ShaderNodeMixRGB');l.new(mask.outputs[0],mix.inputs[0]);mix.inputs[2].default_value=(.9,.9,.9,1)
 if old:l.new(old,mix.inputs[1])
 else:mix.inputs[1].default_value=(rough.default_value,)*3+(1,)
 l.new(mix.outputs[0],rough)
