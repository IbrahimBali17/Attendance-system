import React, { useState } from 'react';
import { ActivityIndicator, Pressable, Text, View } from 'react-native';
import { StatusBar } from 'expo-status-bar';
import { NavigationContainer } from '@react-navigation/native';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { AuthProvider, useAuth } from './src/auth';
import { colors } from './src/theme';
import { t } from './src/i18n';
import LoginScreen from './src/screens/LoginScreen';
import CoursesScreen from './src/screens/CoursesScreen';
import CheckInScreen from './src/screens/CheckInScreen';
import HistoryScreen from './src/screens/HistoryScreen';
import NotificationsScreen from './src/screens/NotificationsScreen';

const Tab = createBottomTabNavigator();

function Tabs() {
  const { logout } = useAuth();
  return (
    <Tab.Navigator
      initialRouteName="CheckIn"
      screenOptions={{
        tabBarActiveTintColor: colors.accent,
        headerTitleStyle: { fontWeight: '800' },
        headerRight: () => (
          <Pressable onPress={logout} style={{ marginRight: 16 }}>
            <Text style={{ color: colors.accent, fontWeight: '700' }}>{t('signOut')}</Text>
          </Pressable>
        ),
        tabBarLabelStyle: { fontWeight: '700', marginBottom: 4 },
        tabBarIcon: () => null,
        tabBarIconStyle: { display: 'none' },
      }}
    >
      <Tab.Screen name="Courses" component={CoursesScreen} options={{ title: t('courses') }} />
      <Tab.Screen name="CheckIn" component={CheckInScreen} options={{ title: t('checkIn') }} />
      <Tab.Screen name="History" component={HistoryScreen} options={{ title: t('history') }} />
      <Tab.Screen name="Alerts" component={NotificationsScreen} options={{ title: t('alerts') }} />
    </Tab.Navigator>
  );
}

function Root() {
  const { user, ready } = useAuth();
  const [, force] = useState(0);
  if (!ready) return <View style={{ flex: 1, justifyContent: 'center' }}><ActivityIndicator size="large" color={colors.accent} /></View>;
  return user ? <Tabs /> : <LoginScreen onLanguageChange={() => force((n) => n + 1)} />;
}

export default function App() {
  return (
    <SafeAreaProvider>
      <AuthProvider>
        <NavigationContainer>
          <StatusBar style="dark" />
          <Root />
        </NavigationContainer>
      </AuthProvider>
    </SafeAreaProvider>
  );
}
