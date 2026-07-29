import {
  BarChart3,
  BookOpen,
  Droplet,
  Eye,
  Heart,
  Home,
  Lightbulb,
  Moon,
  Smile,
  Sparkles,
  Star,
  Shield,
  Sprout,
  TreeDeciduous,
  Users,
  type LucideIcon,
} from 'lucide-react';

const map = {
  eye: Eye,
  heart: Heart,
  lightbulb: Lightbulb,
  sparkles: Sparkles,
  star: Star,
  shield: Shield,
  sprout: Sprout,
  users: Users,
  droplet: Droplet,
  tree: TreeDeciduous,
  moon: Moon,
  book: BookOpen,
  home: Home,
  chart: BarChart3,
  smile: Smile,
} as const;

export type IconName = keyof typeof map;

export function getIcon(name: IconName): LucideIcon {
  return map[name];
}
