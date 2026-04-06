import { Image, type ImageStyle, type StyleProp } from 'react-native';

const source = require('../../assets/icon-small.png');

type Props = {
  style?: StyleProp<ImageStyle>;
  /** Header sağ üst için ~28; splash için daha büyük verilebilir. */
  size?: number;
};

export function BrandIconSmall({ style, size = 28 }: Props) {
  const dim = { width: size, height: size };
  return <Image source={source} style={[dim, style]} resizeMode="contain" accessibilityLabel="Uygulama simgesi" />;
}
