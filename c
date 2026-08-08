<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        xmlns:vm="clr-namespace:GenealogyDiffUtility"
        mc:Ignorable="d"
        x:Class="GenealogyDiffUtility.IndividualEditDialog"
        x:DataType="vm:IndividualEditViewModel"
        x:CompileBindings="False"
        Title="Edit Individual"
        Width="580"
        Height="620"
        WindowStartupLocation="CenterOwner">

    <Grid RowDefinitions="Auto,*,Auto" Margin="20">
        <!-- Dialog Title -->
        <TextBlock Grid.Row="0" Text="{Binding Title}" FontWeight="SemiBold" FontSize="15" Margin="0,0,0,12"/>

        <!-- Main Content Tabs -->
        <TabControl Grid.Row="1" TabStripPlacement="Top">
            <!-- Individual Data (editable) -->
            <TabItem Header="Individual Data">
                <ScrollViewer Padding="10,5">
                    <Grid Margin="10">
                        <Grid.RowDefinitions>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                        </Grid.RowDefinitions>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="110"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>

                        <!-- Full Name -->
                        <TextBlock Grid.Row="0" Grid.Column="0" Text="Full Name:" VerticalAlignment="Center" FontWeight="SemiBold"/>
                        <TextBox Grid.Row="0" Grid.Column="1" Text="{Binding FullName, UpdateSourceTrigger=PropertyChanged}" Margin="5,5"/>

                        <!-- Last Name -->
                        <TextBlock Grid.Row="1" Grid.Column="0" Text="Last Name:" VerticalAlignment="Center" FontWeight="SemiBold"/>
                        <TextBox Grid.Row="1" Grid.Column="1" Text="{Binding LastName, UpdateSourceTrigger=PropertyChanged}" Margin="5,5"/>

                        <!-- Gender -->
                        <TextBlock Grid.Row="2" Grid.Column="0" Text="Gender:" VerticalAlignment="Center" FontWeight="SemiBold"/>
                        <ComboBox Grid.Row="2" Grid.Column="1" Text="{Binding Gender}" Margin="5,5" IsEditable="True">
                            <ComboBoxItem>M</ComboBoxItem>
                            <ComboBoxItem>F</ComboBoxItem>
                            <ComboBoxItem>U</ComboBoxItem>
                        </ComboBox>

                        <!-- Birth Date -->
                        <TextBlock Grid.Row="3" Grid.Column="0" Text="Birth Date:" VerticalAlignment="Center" FontWeight="SemiBold"/>
                        <TextBox Grid.Row="3" Grid.Column="1" Text="{Binding BirthDate, UpdateSourceTrigger=PropertyChanged}" Margin="5,5"/>

                        <!-- Birth Place -->
                        <TextBlock Grid.Row="4" Grid.Column="0" Text="Birth Place:" VerticalAlignment="Center" FontWeight="SemiBold"/>
                        <TextBox Grid.Row="4" Grid.Column="1" Text="{Binding BirthPlace, UpdateSourceTrigger=PropertyChanged}" Margin="5,5"/>

                        <!-- Death Date -->
                        <TextBlock Grid.Row="5" Grid.Column="0" Text="Death Date:" VerticalAlignment="Center" FontWeight="SemiBold"/>
                        <TextBox Grid.Row="5" Grid.Column="1" Text="{Binding DeathDate, UpdateSourceTrigger=PropertyChanged}" Margin="5,5"/>

                        <!-- Death Place -->
                        <TextBlock Grid.Row="6" Grid.Column="0" Text="Death Place:" VerticalAlignment="Center" FontWeight="SemiBold"/>
                        <TextBox Grid.Row="6" Grid.Column="1" Text="{Binding DeathPlace, UpdateSourceTrigger=PropertyChanged}" Margin="5,5"/>
                    </Grid>
                </ScrollViewer>
            </TabItem>

            <!-- Spouses (looked up from family nodes) -->
            <TabItem Header="Spouses">
                <Border Padding="10">
                    <ItemsControl Items="{Binding Spouses}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <Border Padding="8" BorderBrush="#DDDDDD" BorderThickness="0,0,0,1" Background="Transparent">
                                    <StackPanel>
                                        <TextBlock Text="{Binding SpouseName}" FontWeight="SemiBold"/>
                                        <TextBlock Text="{Binding FamilyDisplay}" FontStyle="Italic" Foreground="DimGray" Margin="0,2,0,0"/>
                                    </StackPanel>
                                </Border>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </Border>
            </TabItem>

            <!-- Children (looked up from family nodes) -->
            <TabItem Header="Children">
                <Border Padding="10">
                    <ItemsControl Items="{Binding Children}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <Border Padding="8" BorderBrush="#DDDDDD" BorderThickness="0,0,0,1" Background="Transparent">
                                    <StackPanel>
                                        <TextBlock Text="{Binding ChildName}" FontWeight="SemiBold"/>
                                        <TextBlock Text="{Binding FamilyDisplay}" FontStyle="Italic" Foreground="DimGray" Margin="0,2,0,0"/>
                                    </StackPanel>
                                </Border>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </Border>
            </TabItem>

            <!-- Sources (looked up from source IDs) -->
            <TabItem Header="Sources">
                <Border Padding="10">
                    <ItemsControl Items="{Binding Sources}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <Border Padding="8" BorderBrush="#DDDDDD" BorderThickness="0,0,0,1" Background="Transparent">
                                    <StackPanel>
                                        <TextBlock Text="{Binding Title}" FontWeight="SemiBold"/>
                                        <TextBlock Text="{Binding Author}" Foreground="DimGray" Margin="0,2,0,0"/>
                                        <TextBlock Text="{Binding PublicationInfo}" Foreground="DimGray" Margin="0,2,0,0"/>
                                    </StackPanel>
                                </Border>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </Border>
            </TabItem>

            <!-- Notes (looked up from note IDs) -->
            <TabItem Header="Notes">
                <Border Padding="10">
                    <ItemsControl Items="{Binding Notes}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <Border Padding="8" BorderBrush="#DDDDDD" BorderThickness="0,0,0,1" Background="Transparent">
                                    <TextBlock Text="{Binding DisplayName}" TextWrapping="Wrap"/>
                                </Border>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </Border>
            </TabItem>
        </TabControl>

        <!-- Action Buttons -->
        <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,12,0,0">
            <Button Content="Cancel" Click="OnCancelClick" Width="80" Margin="0,0,10,0"/>
            <Button Content="Save" Click="OnSaveClick" IsDefault="True" Width="80"/>
        </StackPanel>
    </Grid>
</Window>
